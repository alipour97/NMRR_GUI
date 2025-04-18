using NMRR.ViewModels;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NMRR.Helpers
{
    internal class SerialPortService
    {
        const int READ_BUFFER_SIZE = 16 * 1024;
        private readonly SerialPort _serialPort;
        private byte[] buffer = new byte[READ_BUFFER_SIZE]; // Large enough to hold incoming data
        private List<byte> _processingBuffer = new List<byte>();
        private int bufferIndex = 0;
        //private int payloadSize = 
        public event Action<string, byte[]> DataReceived;

        private const string CommandStartMarker = "{";
        private const string CommandEndMarker = ",end}";
        private const string NewLine = "\r\n";
        private const string CommandPattern = "{cmd,\r\n";

        private ConcurrentQueue<byte[]> _receivedDataQueue = new ConcurrentQueue<byte[]>(); // Queue to hold received data, Thread-Safe Data Sharing
        private Task _processingTask;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Command dictionary mapping command types to payload sizes
        static readonly Dictionary<string, int> _commandLengths = new Dictionary<string, int>
    {
        { "fdb", (2* MainViewModel.ADC_CHANNELS) * MainViewModel.ADC_BUFFER_LENGTH * sizeof(UInt32) }, // Time + ADC channel number * data length
        { "cmd1", 100 }, // Example: Custom payload size
        { "cmd2", 20 }, // Another command with 20-byte payload
        // Add other commands here
    };

        public SerialPortService()
        {
            _serialPort = new SerialPort
            {
                PortName = "COM3",
                BaudRate = 1152000,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                NewLine = "\r\n",
                ReadBufferSize = READ_BUFFER_SIZE
            };

            _serialPort.DataReceived += OnDataReceived;
            try
            {
                _serialPort.Open();
                _serialPort.ReadExisting();
                StartDataProcessing();

            }
            catch (Exception ex) { 
            Debug.WriteLine($"Couldn't init serialport: {ex.ToString()}");
            }

        }

        public void StartReceiving()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
            SendData("{start,1}");
        }

        public void StopReceiving()
        {
            if (_serialPort.IsOpen)
            {
                SendData("{start,0}");
            }
        }

        public void SendData(string data)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(data);
            }
            else
            {
                _serialPort.Open();
                _serialPort.Write(data);
            }
        }

        public void SendData(byte[] data)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(data, 0, data.Length);
            }
            else
            {
                _serialPort.Open();
                _serialPort.Write(data, 0, data.Length);
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort.IsOpen)
            {
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    try
                    {
                        int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                        if (bytesRead > 0)
                        {
                            // Only enqueue what we actually read
                            byte[] actualData = new byte[bytesRead];
                            Array.Copy(buffer, actualData, bytesRead);
                            _receivedDataQueue.Enqueue(actualData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading from serial port: {ex.Message}");
                        // Consider implementing reconnection logic if needed
                    }
                }
            }
        }

        private void StartDataProcessing()
        {
            _processingTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        if (_receivedDataQueue.TryDequeue(out byte[] dataChunk))
                        {
                            await ProcessReceivedData(dataChunk);
                        }
                        else
                        {
                            await Task.Delay(1, _cts.Token); // Small delay to avoid busy-waiting
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, no action needed
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in processing task: {ex.Message}");
                }
            }, _cts.Token);
        }

        public async Task StopDataProcessingAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();

                if (_processingTask != null)
                {
                    try
                    {
                        // Use Task.WhenAny to implement a timeout
                        var completedTask = await Task.WhenAny(_processingTask, Task.Delay(5000));
                        if (completedTask != _processingTask)
                        {
                            Debug.WriteLine("Processing task did not complete within timeout.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error stopping processing: {ex.Message}");
                    }
                }

                _cts.Dispose();
                _cts = null;
            }
        }

        private async Task ProcessReceivedData(byte[] dataChunk)
        {
            // Add new data to our processing buffer
            _processingBuffer.AddRange(dataChunk);

            // Keep processing while we have data
            while (_processingBuffer.Count > 0)
            {
                try {
                    // Look for the start of a command
                    if (_processingBuffer.Count >= CommandPattern.Length && Encoding.ASCII.GetString(_processingBuffer.Take(5).ToArray()) == "{fdb,")
                    {
                        // command is fdb
                        if (_processingBuffer.Count >= CommandPattern.Length + _commandLengths["fdb"] + CommandEndMarker.Length + NewLine.Length)
                        {
                            byte[] fdbDataWithEnd = _processingBuffer.Skip(CommandPattern.Length).Take(_commandLengths["fdb"] + CommandEndMarker.Length + NewLine.Length).ToArray();
                            string endMarkerCheck = Encoding.ASCII.GetString(fdbDataWithEnd.Skip(_commandLengths["fdb"]).Take(CommandEndMarker.Length).ToArray());
                            string newLineCheck = Encoding.ASCII.GetString(_processingBuffer.Skip(CommandPattern.Length + _commandLengths["fdb"] + CommandEndMarker.Length).Take(NewLine.Length).ToArray());

                            if (endMarkerCheck == CommandEndMarker && newLineCheck == NewLine) // check if all of fdb receieved
                            {
                                byte[] fdbData = _processingBuffer.Skip(CommandPattern.Length).Take(_commandLengths["fdb"]).ToArray();
                                // Remove received data from the buffer
                                _processingBuffer.RemoveRange(0, CommandPattern.Length + _commandLengths["fdb"] + CommandEndMarker.Length + NewLine.Length);

                                //await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                                //{
                                await Task.Run(() =>
                                {
                                    DataReceived?.Invoke("fdb", fdbData);
                                });
                                continue; // Continue processing
                            }
                            else
                            {
                                string stringBuffer = Encoding.ASCII.GetString(_processingBuffer.ToArray());
                                int index = stringBuffer.IndexOf("{fdb,", 1);
                                if (index > 0)
                                {
                                    string submsg = Encoding.ASCII.GetString(_processingBuffer.Skip(index - 5).Take(10).ToArray());

                                    _processingBuffer.RemoveRange(0, index);
                                    Debug.WriteLine($"fdb wrong length: {index} : {submsg}");
                                }
                            }
                        }
                        else
                        {
                            // Not enough data for complete "fdb" command
                            break;
                        }
                    }

                    // Check for other commands
                    else if (_processingBuffer.Count > 0 && Encoding.ASCII.GetString(_processingBuffer.Take(1).ToArray()) == CommandStartMarker && Encoding.ASCII.GetString(_processingBuffer.Skip(4).Take(3).ToArray()) == ",\r\n")
                    {
                        string BufferString = Encoding.ASCII.GetString(_processingBuffer.ToArray());
                        int endIndex = BufferString.IndexOf(CommandEndMarker + NewLine);
                        if (endIndex > -1)
                        {
                            //we have a complete command
                            byte[] commandBytes = _processingBuffer.Take(endIndex).ToArray();
                            _processingBuffer.RemoveRange(0, endIndex + CommandEndMarker.Length + NewLine.Length);
                            string trimBuffer = BufferString.Substring(CommandStartMarker.Length, endIndex - CommandStartMarker.Length);
                            string[] splits = trimBuffer.Split(',');

                            string command = splits[0];
                            try
                            {
                                if (splits.Length >= 2)
                                {
                                    //await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                                    //{
                                    await Task.Run(() =>
                                    {
                                        DataReceived?.Invoke(command, Encoding.UTF8.GetBytes(splits[1].Substring(NewLine.Length)));
                                    });
                                    continue; // Continue processing
                                }
                                else
                                {
                                    Debug.WriteLine($"Invalid Data {BufferString}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error processing command: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Incomplete command, wait for more data
                            break;
                        }
                    }
                    else
                    {
                        // Discard unknown data
                        string stringBuffer = Encoding.ASCII.GetString(_processingBuffer.ToArray());
                        int idx = stringBuffer.IndexOf(",\r\n");
                        if (stringBuffer[idx - 4] == '{')
                            _processingBuffer.RemoveRange(0, idx - 4);
                        Debug.WriteLine($"Error processing command: {_processingBuffer[0]}");
                        break;
                        //_processingBuffer.RemoveAt(0);
                        // Consider logging discarded bytes
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine($"Problem is: {e}");
                }
                }
        }

        private void OnDataReceived2(object sender, SerialDataReceivedEventArgs e)
        {

            while (_serialPort.BytesToRead > 0)
            {
                if(_serialPort.BytesToRead >= 4)
                {
                    string command = _serialPort.ReadLine();
                    if (command.Length < 4)
                        continue;
                    command = command.Substring(1, 3);
                    if(command.Contains("fdb"))
                    {
                        byte[] fdb_data = new byte[_commandLengths["fdb"]];
                        while (_serialPort.BytesToRead < fdb_data.Length) ;
                        _serialPort.Read(fdb_data, 0, _commandLengths["fdb"]);
                        _serialPort.ReadExisting();
                        DataReceived?.Invoke(command, fdb_data);
                    }
                    else if(command.Contains("inf"))
                    {
                        string info = _serialPort.ReadLine();

                        DataReceived?.Invoke(command, Encoding.UTF8.GetBytes(info[..^5]));
                    }
                    else if(command.Contains("dac"))
                    {
                        //Thread.Sleep(10);
                        string msg = _serialPort.ReadLine();
                        _serialPort.ReadExisting();
                        DataReceived?.Invoke(command, Encoding.UTF8.GetBytes(msg[..^5]));
                        //
                    }
                    else if(command.Contains("cmd"))
                    {
                        string info = _serialPort.ReadLine();

                        DataReceived?.Invoke(command, Encoding.UTF8.GetBytes(info[..^5]));
                    }
                }

            }
            
        }
    }
}
