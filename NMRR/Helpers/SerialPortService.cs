using NMRR.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace NMRR.Helpers
{
    internal class SerialPortService
    {
        private readonly SerialPort _serialPort;
        private byte[] buffer = new byte[4096]; // Large enough to hold incoming data
        private int bufferIndex = 0;
        //private int payloadSize = 
        public event Action<string, byte[]> DataReceived;

        // Command dictionary mapping command types to payload sizes
        static readonly Dictionary<string, int> commandLengths = new Dictionary<string, int>
    {
        { "fdb", 7 + (2* MainViewModel.ADC_CHANNELS) * MainViewModel.ADC_BUFFER_LENGTH * sizeof(UInt32) }, // Time + ADC channel number * data length
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
                NewLine = "\r\n"
            };

            _serialPort.DataReceived += OnDataReceived;
            try
            {
                _serialPort.Open();
                _serialPort.ReadExisting();
            }
            catch (Exception ex) { }



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
                        byte[] fdb_data = new byte[commandLengths["fdb"]];
                        while (_serialPort.BytesToRead < fdb_data.Length) ;
                        _serialPort.Read(fdb_data, 0, commandLengths["fdb"]);
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
                //int bytesAvailable = _serialPort.BytesToRead;
                //byte[] incomingData = new byte[bytesAvailable];
                //_serialPort.Read(incomingData, 0, bytesAvailable);

                //// Append incoming data to the persistent buffer
                //if (bytesAvailable > 4096 - bufferIndex)
                //{
                //    bufferIndex = 0;
                //    buffer = new byte[4096];
                //}
                //Array.Copy(incomingData, 0, buffer, bufferIndex, bytesAvailable);
                //bufferIndex += bytesAvailable;

                //string bufferString = Encoding.UTF8.GetString(buffer, 0, bufferIndex);
                //if (bufferString.Contains('{'))
                //{
                //    string command = "";
                //    if (bufferString.Contains("fdb"))
                //    {
                //        if (bufferIndex < 400)
                //            continue;
                //        command = "fdb";
                //        int dataLength = commandLengths[command];
                //        byte[] fdb_data = new byte[dataLength];
                //        Array.Copy(buffer, command.Length + 2, fdb_data, 0, dataLength);
                        
                //        reset_uart();
                //    }

                //    else if (bufferString.Contains("inf"))
                //    {
                //        string[] splits = bufferString.Split(',');
                //        if (splits.Length < 3)
                //            continue;
                //        command = splits[0].Substring(1);
                //        DataReceived?.Invoke(command, Encoding.ASCII.GetBytes(splits[1]));
                //        reset_uart();
                //    }
                //}
                // Process any complete messages in the buffer
                //ProcessBuffer();



            }
            
        }

        //private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        //{

        //    while (_serialPort.BytesToRead > 0)
        //    {

        //        int bytesAvailable = _serialPort.BytesToRead;
        //        byte[] incomingData = new byte[bytesAvailable];
        //        _serialPort.Read(incomingData, 0, bytesAvailable);

        //        // Append incoming data to the persistent buffer
        //        if(bytesAvailable > 4096 - bufferIndex)
        //        {
        //            bufferIndex = 0;
        //            buffer = new byte[4096];
        //        }
        //        Array.Copy(incomingData, 0, buffer, bufferIndex, bytesAvailable);
        //        bufferIndex += bytesAvailable;

        //        string bufferString = Encoding.UTF8.GetString(buffer, 0,bufferIndex);
        //        if (bufferString.Contains('{'))
        //        {
        //            string command = "";
        //            if (bufferString.Contains("fdb"))
        //            {
        //                if (bufferIndex < 400) 
        //                    continue;
        //                command = "fdb";
        //                int dataLength = commandLengths[command];
        //                byte[] fdb_data = new byte[dataLength];
        //                Array.Copy(buffer, command.Length + 2, fdb_data, 0, dataLength);
        //                DataReceived?.Invoke(command, fdb_data);
        //                reset_uart();
        //            }

        //            else if(bufferString.Contains("inf"))
        //            {
        //                string[] splits = bufferString.Split(',');
        //                if (splits.Length < 3)
        //                    continue;
        //                command = splits[0].Substring(1);
        //                DataReceived?.Invoke(command, Encoding.ASCII.GetBytes(splits[1]));
        //                reset_uart();
        //            }
        //        }
        //        // Process any complete messages in the buffer
        //        //ProcessBuffer();



        //    }
        //        //byte[] temp = new byte[_serialPort.BytesToRead];
        //        //_serialPort.Read(temp, 0, _serialPort.BytesToRead);
        //        //data_in.AddRange(temp);
        //        //if (data_in[0] == '{')
        //        //{
        //        //    while (_serialPort.BytesToRead > 0)
        //        //    {
        //        //        byte[] temp2 = new byte[_serialPort.BytesToRead];
        //        //        _serialPort.Read(temp2, 0, _serialPort.BytesToRead);
        //        //        data_in.AddRange(temp2);
        //        //        //if (data_in.Last() == '\n' && data_in[data_in.Count - 2] == '\r')
        //        //        if(data_in.Contains((byte)'}'))
        //        //        {
        //        //            DataReceived?.Invoke(data_in.ToArray());
        //        //            data_in.Clear();
        //        //            return;
        //        //        }
        //        //    }
        //        //}



        //        //byte[] temp = new byte[_serialPort.BytesToRead];
        //        //_serialPort.Read(temp, 0, _serialPort.BytesToRead);
        //        ////var data = _serialPort.ReadLine();
        //        //data_in.AddRange(temp);
        //        //int length = data_in.Count;
        //        //if (length > 3 && (data_in[length-1] == '\n') && (data_in[length - 2] == '\r') && (data_in[length - 3] == ' '))
        //        //{
        //        //    DataReceived?.Invoke(data_in.ToArray());
        //        //    data_in.Clear();
        //        //    return;
        //        //}


        //}

        private void ProcessBuffer()
        {
            int startIndex = 0;

            while (startIndex < bufferIndex)
            {
                
                // Step 1: Look for '{'
                if (buffer[startIndex] != (byte)'{')
                {
                    startIndex++; // Skip invalid data
                    continue;
                }

                // Step 2: Ensure there are enough bytes for at least a command
                int commandIndex = Array.IndexOf(buffer, (byte)',', startIndex);
                if (commandIndex == -1)
                {
                    // Command Seperator ',' not found, wait for more data
                    break;
                }

                // Step 3: Extract command type (3 bytes after '{')
                string command = Encoding.ASCII.GetString(buffer, startIndex + 1, commandIndex - startIndex - 1);
                string bufferString = Encoding.ASCII.GetString(buffer);
                // Step 4: Look for the closing '}'
                if (!bufferString.Contains(",end}"))
                    break;
                int endIndex = bufferString.IndexOf(",end}");
                if (endIndex == -1)
                {
                    // Closing '}' not found, wait for more data
                    break;
                }

                // Special case: Handle `inf` command with unknown length
                //if (command == "inf")
                //{
                // Extract the complete command including 'inf' and data
                int payloadLength = endIndex - commandIndex - 1;
                if (command == "fb" && payloadLength < 400)
                    reset_uart();
                byte[] payload = new byte[payloadLength];
                Array.Copy(buffer, commandIndex + 1, payload, 0, payloadLength);
                //string infoString = Encoding.ASCII.GetString(completepayload);
                // Process the `inf` command
                DataReceived?.Invoke(command, payload);
                startIndex = 0;
                reset_uart();
                // Move startIndex past the current command
                //startIndex += commandLength;
                //continue;
                //}

                //// for known data length
                //if (!commandLengths.ContainsKey(command))
                //{
                //    startIndex++; // Skip invalid command
                //    continue;
                //}

                //// Step 5: Determine required length for this command
                //int payloadSize = commandLengths[command];
                //int requiredLength = 4 + payloadSize; // 4 bytes = '{' + command (3 bytes)

                //// Step 6: Extract payload
                //byte[] payload = new byte[payloadSize];
                //Array.Copy(buffer, startIndex + 4, payload, 0, payloadSize);

                //// Process the payload
                ////ProcessPayload(payload);
                //DataReceived?.Invoke(command, payload);
                //// Move startIndex past the current command
                //startIndex += requiredLength;
            }

            // Retain leftover data in the buffer
            if (startIndex < bufferIndex)
            {
                Array.Copy(buffer, startIndex, buffer, 0, bufferIndex - startIndex);
            }
            bufferIndex -= startIndex;

        }

        private void reset_uart()
        {
            buffer = new byte[4096];
            bufferIndex = 0;
        }
    }
}
