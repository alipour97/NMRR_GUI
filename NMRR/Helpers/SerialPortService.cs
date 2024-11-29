using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace NMRR.Helpers
{
    internal class SerialPortService
    {
        private readonly SerialPort _serialPort;

        public event Action<string> DataReceived;

        public SerialPortService()
        {
            _serialPort = new SerialPort
            {
                PortName = "COM3",
                BaudRate = 230400,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                NewLine = "\r\n"
            };

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
        }

        public void StartReceiving()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
            SendData("{send,1}");
        }

        public void StopReceiving()
        {
            if (_serialPort.IsOpen)
            {
                SendData("{send,0}");
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

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = _serialPort.ReadLine();
            DataReceived?.Invoke(data);
        }
    }
}
