using System;
using System.IO;
using System.Net;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;

namespace PMSender
{
    class PMCore
    {
        const string addr = "http://nya.ac.cn:5000/api/set";
        const string name = "rpi";

        SerialPort port;
        List<byte> buffer;
        Timer timer;

        bool locked;

        public int PM25 { get; set; }
        public int PM10 { get; set; }

        public PMCore()
        {
            port = new SerialPort();
            port.BaudRate = 9600;
            buffer = new List<byte>(10);
            timer = new Timer(CoreWork);
        }

        public string[] GetPorts()
        {
            return SerialPort.GetPortNames();
        }

        public void Connect(string name)
        {
            port.PortName = name;
            try
            {
                if (!port.IsOpen) port.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            locked = false;
            timer.Change(0, 1000);
        }

        public void DisConnect()
        {
            timer.Change(Timeout.Infinite, 1000);
            locked = false;
            if (port.IsOpen) port.Close();
        }

        protected bool CheckPacket(byte[] dataBuf)
        {
            if (dataBuf[9] != 0xAB) return false;
            byte sum = 0;
            for (int i = 2; i <= 7; i++)
                sum += dataBuf[i];
            return sum == dataBuf[8];
        }

        protected void UploadData(string url)
        {
            try
            {
                Stream stream = new WebClient().OpenRead(url);
                string str = new StreamReader(stream).ReadToEnd();
                str = str.Replace("{", "").Replace("}", "").Split(',')[0];
                Console.Write(str);
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }

        protected void ShowData()
        {
            Console.Write("PM2.5: " + PM25 + ", PM10: " + PM10 + ". ");

            string url;

            url = addr +
                  "~id=" + name +
                  "&time=" + DateTime.Now.ToString().Replace(" ", "T").Replace("/", ".") +
                  "&pm25=" + PM25 +
                  "&pm10=" + PM10;

            Console.Write("Uploading... ");
            UploadData(url);

            Console.Write('\r');
        }

        protected void CoreWork(object obj)
        {
            byte tmp;

            if (port.IsOpen && !locked)
            {
                locked = true;
                while (port.BytesToRead > 0)
                {
                    tmp = (byte)port.ReadByte();
                    if (tmp == 0xAA)
                    {
                        buffer.Clear();
                        buffer.Add(tmp);
                        while (true)
                        {
                            tmp = (byte)port.ReadByte();
                            buffer.Add(tmp);
                            if (tmp == 0xAB || buffer.ToArray().Length == buffer.Capacity) break;
                        }
                        port.ReadExisting();

                        if (buffer.ToArray().Length != buffer.Capacity) goto END;
                        if (!CheckPacket(buffer.ToArray())) goto END;

                        PM25 = (buffer[3] * 256 + buffer[2]) / 10;
                        PM10 = (buffer[5] * 256 + buffer[4]) / 10;

                        ShowData();
                    }
                }
                END:
                locked = false;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            PMCore core = new PMCore();

            Console.WriteLine("PMSender v0.01\n");

            if (core.GetPorts().Length > 1)
            {
                Console.WriteLine("Ports below:");
                foreach (string p in core.GetPorts())
                    Console.Write(p + " ");
                Console.WriteLine("\n");
                Console.Write("Input port: ");
                core.Connect(Console.ReadLine());
            }
            else if (core.GetPorts().Length == 1)
            {
                core.Connect(core.GetPorts()[0]);
            }
            else
            {
                Console.WriteLine("No port connected");
            }

            Console.WriteLine("\nPress ESC to exit.\n");
            while (Console.ReadKey(true).Key != ConsoleKey.Escape);
            core.DisConnect();
        }
    }
}
