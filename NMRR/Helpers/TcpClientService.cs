using NMRR.ViewModels; // Assuming this namespace is still relevant
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets; // Required for TCP
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// using System.Windows.Threading; // Keep if UI thread marshalling is truly needed later

namespace NMRR.Helpers // Or your preferred namespace
{
    internal class TcpClientService : IDisposable
    {
        // --- Configuration ---
        private readonly string _serverIpAddress;
        private readonly int _serverPort;
        const int READ_BUFFER_SIZE = 16 * 1024; // Same buffer size for reading from the socket

        // --- TCP Client and Stream ---
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;

        // --- Data Buffering and Processing (Largely unchanged) ---
        private List<byte> _processingBuffer = new List<byte>();
        public event Action<string, byte[]> DataReceived; // Same event signature

        private const string CommandStartMarker = "{";
        private const string CommandEndMarker = ",end}";
        private const string NewLine = "\r\n";
        private const string CommandPattern = "{fdb,\r\n"; // Check if need to adjust length '{fdb,'

        private ConcurrentQueue<byte[]> _receivedDataQueue = new ConcurrentQueue<byte[]>(); // Queue to hold received data chunks
        private Task _processingTask;
        private Task _receivingTask; // Dedicated task for reading from the socket
        private CancellationTokenSource _cts; // Combined cancellation for both tasks

        // Command dictionary mapping command types to payload sizes (Unchanged)
        static readonly Dictionary<string, int> _commandLengths = new Dictionary<string, int>
        {
            { "fdb", (2 * MainViewModel.ADC_CHANNELS) * MainViewModel.ADC_BUFFER_LENGTH * sizeof(UInt32) },
            { "cmd1", 100 },
            { "cmd2", 20 },
            // Add other commands here
        };

        // --- State ---
        public bool IsConnected => _tcpClient?.Connected ?? false;
        private readonly object _connectionLock = new object(); // To prevent race conditions on connect/disconnect

        // --- Constructor ---
        public TcpClientService(string serverIp, int serverPort)
        {
            _serverIpAddress = serverIp;
            _serverPort = serverPort;
            // Initialize _tcpClient here or in ConnectAsync
        }

        // --- Connection Management ---
        public async Task<bool> ConnectAsync()
        {
            lock (_connectionLock)
            {
                if (IsConnected)
                {
                    Debug.WriteLine("Already connected.");
                    return true; // Or false if reconnecting isn't desired
                }
                // Ensure previous resources are cleaned up if any attempt failed partially
                CleanUpResources();
            }

            try
            {
                _cts = new CancellationTokenSource();
                _tcpClient = new TcpClient();
                // Optional: Configure NoDelay, KeepAlive, buffer sizes if needed
                //_tcpClient.NoDelay = true;
                // _tcpClient.ReceiveBufferSize = READ_BUFFER_SIZE * 2; // Example

                Debug.WriteLine($"Attempting to connect to {_serverIpAddress}:{_serverPort}...");
                await _tcpClient.ConnectAsync(_serverIpAddress, _serverPort);
                Debug.WriteLine("Connected successfully.");

                _networkStream = _tcpClient.GetStream();

                // Start the background tasks for receiving and processing
                _receivingTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
                _processingTask = Task.Run(() => ProcessQueueLoopAsync(_cts.Token), _cts.Token);

                return true;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"TCP Connection Error: {ex.Message} (SocketErrorCode: {ex.SocketErrorCode})");
                CleanUpResources();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting: {ex.Message}");
                CleanUpResources();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            lock (_connectionLock)
            {
                if (!IsConnected && _cts == null) // Already disconnected or never connected
                {
                    Debug.WriteLine("Not connected or already disconnected.");
                    return;
                }
            }

            Debug.WriteLine("Disconnecting...");
            // 1. Signal cancellation
            if (_cts != null)
            {
                _cts.Cancel();
                Debug.WriteLine("Cancellation signaled.");
            }

            // 2. Wait for tasks to complete (with timeout)
            List<Task> tasksToWait = new List<Task>();
            if (_receivingTask != null && !_receivingTask.IsCompleted) tasksToWait.Add(_receivingTask);
            if (_processingTask != null && !_processingTask.IsCompleted) tasksToWait.Add(_processingTask);

            if (tasksToWait.Any())
            {
                var allTasks = Task.WhenAll(tasksToWait);
                try
                {
                    // Wait slightly longer than the internal delays + potential blocking time
                    if (await Task.WhenAny(allTasks,Task.Delay(2000)) == allTasks)
                    {
                        Debug.WriteLine("Warning: Disconnect timeout. Not all tasks finished gracefully.");
                    }
                    else
                    {
                        Debug.WriteLine("Receiving and Processing tasks stopped.");
                    }
                }
                catch (OperationCanceledException) { /* Expected */ Debug.WriteLine("Tasks cancelled during disconnect."); }
                catch (Exception ex) { Debug.WriteLine($"Exception while waiting for tasks during disconnect: {ex}"); } // Log unexpected errors
            }

            // 3. Clean up resources
            lock (_connectionLock) // Ensure atomicity of resource cleanup
            {
                CleanUpResources();
                // Clear queue just in case processing stopped mid-way
                _receivedDataQueue = new ConcurrentQueue<byte[]>();
                _processingBuffer.Clear();
                Debug.WriteLine("Resources cleaned up.");
            }
        }

        // --- Data Transmission ---
        public async Task SendDataAsync(string data)
        {
            if (!IsConnected || _networkStream == null)
            {
                Debug.WriteLine("Cannot send data: Not connected.");
                // Optionally, throw an exception or return false
                return;
            }

            try
            {
                byte[] buffer = Encoding.ASCII.GetBytes(data); // Or UTF8 if needed by server
                await _networkStream.WriteAsync(buffer, 0, buffer.Length, _cts.Token); // Use cancellation token
                //await _networkStream.FlushAsync(); // Often not necessary with TCP, but can ensure data is sent immediately
            }
            catch (ObjectDisposedException) { Debug.WriteLine("Cannot send data: Connection closed."); await HandleDisconnectError(); }
            // Catch IOException specifically for network related errors
            catch (IOException ex) { Debug.WriteLine($"IO Error sending data: {ex.Message}"); await HandleDisconnectError(); }
            catch (OperationCanceledException) { Debug.WriteLine("Send operation cancelled."); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending string data: {ex.Message}");
                // Consider disconnecting on unexpected errors
                await HandleDisconnectError();
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!IsConnected || _networkStream == null)
            {
                Debug.WriteLine("Cannot send data: Not connected.");
                return;
            }

            try
            {
                await _networkStream.WriteAsync(data, 0, data.Length, _cts.Token); // Use cancellation token
                //await _networkStream.FlushAsync();
            }
            catch (ObjectDisposedException) { Debug.WriteLine("Cannot send data: Connection closed."); await HandleDisconnectError(); }
            catch (IOException ex) { Debug.WriteLine($"IO Error sending data: {ex.Message}"); await HandleDisconnectError(); }
            catch (OperationCanceledException) { Debug.WriteLine("Send operation cancelled."); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending byte data: {ex.Message}");
                await HandleDisconnectError();
            }
        }

        // --- Receiving Loop (Replaces SerialPort.DataReceived) ---
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[READ_BUFFER_SIZE];
            Debug.WriteLine("Receive loop started.");

            try
            {
                while (!token.IsCancellationRequested && IsConnected && _networkStream != null)
                {
                    // Check CanRead before attempting to read
                    if (!_networkStream.CanRead)
                    {
                        Debug.WriteLine("Network stream cannot be read. Assuming disconnect.");
                        break; // Exit loop if stream is not readable
                    }

                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (bytesRead > 0)
                    {
                        byte[] actualData = new byte[bytesRead];
                        Array.Copy(buffer, actualData, bytesRead);
                        _receivedDataQueue.Enqueue(actualData); // Enqueue the chunk for processing
                    }
                    else // bytesRead == 0
                    {
                        // Graceful shutdown by the server
                        Debug.WriteLine("Server closed the connection (ReadAsync returned 0).");
                        break; // Exit loop
                    }
                }
            }
            catch (ObjectDisposedException) { Debug.WriteLine("Receive loop stopped: Connection closed (ObjectDisposedException)."); }
            // Catch IOException for network errors during read
            catch (IOException ex) { Debug.WriteLine($"Receive loop stopped: IO Error reading from socket: {ex.Message}"); }
            catch (OperationCanceledException) { Debug.WriteLine("Receive loop cancelled."); }
            catch (Exception ex)
            {
                // Catch unexpected errors
                Debug.WriteLine($"Receive loop terminated unexpectedly: {ex.Message}");
            }
            finally
            {
                // If the loop exits (for any reason other than explicit cancellation),
                // trigger the disconnect process to ensure cleanup.
                if (!token.IsCancellationRequested)
                {
                    Debug.WriteLine("Receive loop ended unexpectedly or due to connection close. Triggering cleanup.");
                    // Don't await here to avoid deadlocks if DisconnectAsync tries to wait for this task
                    _ = Task.Run(DisconnectAsync);
                }
                Debug.WriteLine("Receive loop finished.");
            }
        }

        // --- Processing Loop (Largely unchanged logic, different trigger) ---
        private async Task ProcessQueueLoopAsync(CancellationToken token)
        {
            Debug.WriteLine("Processing loop started.");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_receivedDataQueue.TryDequeue(out byte[] dataChunk))
                    {
                        // Process the received chunk. The core parsing logic is the same.
                        await ProcessReceivedDataChunk(dataChunk);
                    }
                    else
                    {
                        // Wait politely if the queue is empty
                        await Task.Delay(10, token); // Small delay to avoid tight loop when idle
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Processing loop cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in processing task: {ex.Message}");
                // Consider if an error here should trigger disconnect
            }
            finally
            {
                Debug.WriteLine("Processing loop finished.");
            }
        }

        // --- Data Parsing Logic (Moved chunk adding here, parsing logic unchanged) ---
        private async Task ProcessReceivedDataChunk(byte[] dataChunk)
        {
            // Add new data to our processing buffer
            _processingBuffer.AddRange(dataChunk);
            //Debug.WriteLine($"Added {dataChunk.Length} bytes to processing buffer. Total: {_processingBuffer.Count}");


            // Keep processing the buffer while we might have complete commands
            bool processedSomething = true;
            while (_processingBuffer.Count > 0 && processedSomething)
            {
                processedSomething = false; // Assume we won't process anything in this iteration unless proven otherwise
                try
                {
                    // --- Your existing parsing logic from ProcessReceivedData goes here ---
                    // --- Make sure it uses _processingBuffer correctly ---

                    // Look for the start of a command "{fdb,"
                    int fdbCmdLength = CommandPattern.Length; // Length of "{fdb,\r\n"
                    if (_processingBuffer.Count >= fdbCmdLength && Encoding.ASCII.GetString(_processingBuffer.Take(fdbCmdLength).ToArray()) == CommandPattern)
                    {
                        int expectedFdbLength = _commandLengths["fdb"];
                        int fdbEndMarkerLength = CommandEndMarker.Length;
                        int newLineLength = NewLine.Length;

                        int totalPacketLength = fdbCmdLength + expectedFdbLength + fdbEndMarkerLength + newLineLength;

                        if (_processingBuffer.Count >= totalPacketLength)
                        {
                            // Extract potential end marker and newline for verification
                            byte[] potentialEndMarkerBytes = _processingBuffer.Skip(fdbCmdLength + expectedFdbLength).Take(fdbEndMarkerLength).ToArray();
                            byte[] potentialNlBytes = _processingBuffer.Skip(fdbCmdLength + expectedFdbLength + fdbEndMarkerLength).Take(newLineLength).ToArray();

                            string endMarkerCheck = Encoding.ASCII.GetString(potentialEndMarkerBytes);
                            string newLineCheck = Encoding.ASCII.GetString(potentialNlBytes);

                            if (endMarkerCheck == CommandEndMarker && newLineCheck == NewLine)
                            {
                                // Valid "fdb" command found
                                byte[] fdbData = _processingBuffer.Skip(fdbCmdLength).Take(expectedFdbLength).ToArray();

                                // Remove the processed command from the buffer
                                _processingBuffer.RemoveRange(0, totalPacketLength);
                                //Debug.WriteLine($"Processed fdb command. {_processingBuffer.Count} bytes remaining in buffer.");

                                // Invoke the event (consider marshalling if UI update needed)
                                // Using Task.Run avoids blocking the processing loop, similar to original
                                await Task.Run(() => DataReceived?.Invoke("fdb", fdbData)); // No ConfigureAwait(false) needed in Task.Run lambda
                                processedSomething = true; // We successfully processed a command
                                continue; // Process next potential command in the buffer
                            }
                            else
                            {
                                // Mismatch in end marker or newline - data corruption or framing error
                                Debug.WriteLine($"FDB detected, but end marker ('{endMarkerCheck}') or newline ('{newLineCheck}') mismatch. Discarding first byte.");
                                // Simple error handling: discard the first byte and try again
                                // More robust: search for the next possible '{'
                                _processingBuffer.RemoveAt(0);
                                // No 'continue' here, loop again to re-evaluate buffer state
                            }
                        }
                        else
                        {
                            // Not enough data for a complete "fdb" command yet
                            Debug.WriteLine($"Partial fdb command detected. Need {totalPacketLength}, have {_processingBuffer.Count}. Waiting for more data.");
                            break; // Exit the inner while loop, wait for more data in the outer loop
                        }
                    }
                    // Check for other commands (general pattern: {cmd, PAYLOAD ,end} \r\n)
                    else if (_processingBuffer.Count > 0) // Check if there's any data left
                    {
                        // Convert to string cautiously, might be slow for large buffers or contain invalid chars
                        // It's often better to work with byte arrays if possible, but the original code used IndexOf
                        string bufferAsString = Encoding.ASCII.GetString(_processingBuffer.ToArray()); // Potential for exceptions if non-ASCII data

                        // Find the end sequence ({cmd...},end}\r\n)
                        string endSequence = CommandEndMarker + NewLine;
                        int endIndex = bufferAsString.IndexOf(endSequence);

                        if (endIndex != -1)
                        {
                            // Found a potential end marker. Now check if it starts correctly.
                            string potentialCommand = bufferAsString.Substring(0, endIndex);
                            if (potentialCommand.StartsWith(CommandStartMarker) && potentialCommand.Contains(",")) // Basic check for '{' and ','
                            {
                                // Extract command type and payload
                                string trimmedCmd = potentialCommand.Substring(CommandStartMarker.Length);
                                string[] parts = trimmedCmd.Split(new[] { ',' }, 2); // Split only on the first comma

                                if (parts.Length == 2)
                                {
                                    string commandType = parts[0].Trim();
                                    // Assuming payload starts after ",\r\n"
                                    string payloadWithNewline = parts[1].TrimStart(); // Remove leading spaces if any
                                    string payload;
                                    if (payloadWithNewline.StartsWith(NewLine))
                                    {
                                        payload = payloadWithNewline.Substring(NewLine.Length);
                                    }
                                    else
                                    {
                                        // This case might indicate a formatting issue from sender
                                        //Debug.WriteLine($"Warning: Payload for command '{commandType}' doesn't start immediately with NewLine. Payload part: '{payloadWithNewline}'");
                                        payload = payloadWithNewline; // Use as is, might be wrong
                                    }


                                    // Remove the processed command + end sequence from buffer
                                    int totalCommandLength = endIndex + endSequence.Length;
                                    _processingBuffer.RemoveRange(0, totalCommandLength);
                                    //Debug.WriteLine($"Processed generic command '{commandType}'. {_processingBuffer.Count} bytes remaining.");


                                    // Invoke event
                                    byte[] payloadBytes = Encoding.ASCII.GetBytes(payload); // Or UTF8 if applicable
                                    await Task.Run(() => DataReceived?.Invoke(commandType, payloadBytes));
                                    processedSomething = true; // We processed a command
                                    continue; // Process next potential command
                                }
                                else
                                {
                                    Debug.WriteLine($"Invalid command structure found: {potentialCommand}. Discarding first byte.");
                                    _processingBuffer.RemoveAt(0);
                                }
                            }
                            else
                            {
                                // Didn't start with '{' or contain ',', likely corrupt data before the end marker
                                Debug.WriteLine($"Found end marker, but start is invalid or corrupt. Discarding data up to end marker.");
                                // Discard everything up to and including the found end sequence
                                _processingBuffer.RemoveRange(0, endIndex + endSequence.Length);
                                processedSomething = true; // We removed data
                                continue;
                            }
                        }
                        else
                        {
                            // No complete command end marker found in the buffer yet
                            // Optional: Check if buffer is getting excessively large without finding a marker
                            if (_processingBuffer.Count > READ_BUFFER_SIZE * 4) // Example threshold
                            {
                                Debug.WriteLine($"Warning: Processing buffer large ({_processingBuffer.Count} bytes) without finding command end marker. Clearing buffer to prevent memory issues.");
                                _processingBuffer.Clear(); // Or implement smarter partial discard
                            }
                            break; // Wait for more data
                        }
                    }
                    else
                    {
                        // Buffer became empty during processing iteration
                        break;
                    }

                    //--- End of your parsing logic ---
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Debug.WriteLine($"Parsing Error (ArgumentOutOfRange): {ex.Message}. Buffer size: {_processingBuffer.Count}. Attempting recovery by discarding first byte.");
                    if (_processingBuffer.Count > 0) _processingBuffer.RemoveAt(0);
                    processedSomething = true; // We took recovery action
                }
                catch (DecoderFallbackException ex)
                {
                    Debug.WriteLine($"Parsing Error (ASCII Decoding): {ex.Message}. Buffer might contain non-ASCII data. Discarding first byte.");
                    if (_processingBuffer.Count > 0) _processingBuffer.RemoveAt(0);
                    processedSomething = true; // We took recovery action
                                               // Consider more robust handling if binary + ASCII mixed is expected
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Generic Error processing buffer: {ex.Message}");
                    // Decide on recovery strategy. Clearing buffer might be too drastic.
                    // Maybe discard the first byte as a simple recovery attempt.
                    if (_processingBuffer.Count > 0) _processingBuffer.RemoveAt(0);
                    processedSomething = true; // We took recovery action
                    // Consider breaking the loop or logging more details
                }
            } // End while (_processingBuffer.Count > 0 && processedSomething)
        }


        // --- Helper Methods ---
        private void CleanUpResources()
        {
            // Use null-conditional operator ?. for safety
            _networkStream?.Close(); // Close the stream first
            _networkStream?.Dispose();
            _networkStream = null;

            _tcpClient?.Close(); // Then close the client
            _tcpClient?.Dispose();
            _tcpClient = null;

            _cts?.Dispose(); // Dispose cancellation token source
            _cts = null;

            // Tasks are not disposable, just ensure they are completed/cancelled
            _receivingTask = null;
            _processingTask = null;

            Debug.WriteLine("TCP resources cleaned up.");
        }

        // Helper to handle disconnection events consistently
        private async Task HandleDisconnectError()
        {
            Debug.WriteLine("Network error detected, initiating disconnect sequence.");
            // Fire and forget disconnect to avoid deadlocking if called from IO thread
            _ = Task.Run(DisconnectAsync);
            // Optionally, raise an event to notify the UI/ViewModel about the disconnection
            // Disconnected?.Invoke();
        }

        // --- IDisposable ---
        public void Dispose()
        {
            // Ensure disconnect is called on dispose
            // Use .Wait() or .GetAwaiter().GetResult() carefully if called from non-async context like Dispose
            // Or better, make the application ensure DisconnectAsync is called before disposing
            // Running async void or Task.Run here is generally discouraged in Dispose
            try
            {
                // Initiate cancellation and resource cleanup synchronously if possible
                _cts?.Cancel(); // Signal cancellation first
                CleanUpResources(); // Clean up resources directly
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during Dispose cleanup: {ex.Message}");
            }
            GC.SuppressFinalize(this); // Prevent finalizer from running
        }

        // Optional: Finalizer as a safeguard (use cautiously)
        // ~TcpClientService()
        // {
        //     Debug.WriteLine("Warning: TcpClientService finalizer called. Dispose() was not called properly.");
        //     CleanUpResources();
        // }

        // --- Methods mimicking SerialPortService (Adapt signatures if needed) ---

        // StartReceiving/StopReceiving now map more closely to Connect/Disconnect
        // but can also be used to send specific start/stop commands if the *server* requires them

        public async Task StartReceivingAsync() // Renamed to Async
        {
            // Ensure connected first
            if (!IsConnected)
            {
                if (!await ConnectAsync())
                {
                    Debug.WriteLine("Failed to connect. Cannot send start command.");
                    return;
                }
                // Optional delay after connection before sending command
                // await Task.Delay(100);
            }
            // Send the application-level start command
            await SendDataAsync("{start,1}");
        }

        public async Task StopReceivingAsync() // Renamed to Async
        {
            // Send the application-level stop command *before* disconnecting
            // Only send if actually connected to avoid errors
            if (IsConnected)
            {
                await SendDataAsync("{start,0}");
                // Optional: Wait a moment for the command to be processed by the server
                // await Task.Delay(100);
            }
            else
            {
                Debug.WriteLine("Not connected. Cannot send stop command.");
            }
            // Note: This method *doesn't* disconnect. Call DisconnectAsync separately if needed.
        }

        public async Task EmergencyStopBtnAsync()
        {
            await SendDataAsync("{emg_stop,1}");
        }
        public async Task ReleaseEmergencyAsync()
        {
            await SendDataAsync("{emg_stop,0}");
        }
    }
}
