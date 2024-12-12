using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
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
                BaudRate = 115200,
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
            List<byte> data_in = new List<byte>();
            while (_serialPort.BytesToRead > 0)
            {
                byte[] temp = new byte[_serialPort.BytesToRead];
                _serialPort.Read(temp, 0, _serialPort.BytesToRead);
                //var data = _serialPort.ReadLine();
                data_in.AddRange(temp);
                int length = data_in.Count;
                if ((data_in[length-1] == '\n') && (data_in[length - 2] == '\r') && (data_in[length - 3] == '}'))
                {
                    DataReceived?.Invoke(data_in.ToArray());
                    continue;
                }
                
            }
        }
    }
}
