using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http;
using System.Web.Http;
using System.Net.Mail;
using System.IO;
using System.Web.Script.Serialization;

namespace WiWo.Controllers
{
    public class UIController : ApiController
    {
        /// <summary>
        /// Socket class
        /// </summary>
        public class Socket
        {
            public IPAddress ipAddress;
            public byte[] macAddress;
            public int port;

            public string name
            {
                get; private set;
            }

            public bool isOn
            {
                get; private set;
            }

            public DateTime subscribedTime
            {
                get; private set;
            }

            public bool subscribed
            {
                get; private set;
            }

            public byte[] data
            {
                get; private set;
            }

            #region socket data
            private byte[] discoverData = {
                0x68, 0x64, 0x00, 0x12, 0x71, 0x67,
                0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20
            };

            private byte[] subData = {
                0x68, 0x64, 0x00, 0x1e, 0x63, 0x6c,
                0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
                0x12, 0xac, 0x8d, 0x23, 0xcf, 0xac,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20
            };

            private byte[] powerOnData = {
                0x68, 0x64, 0x00, 0x17, 0x64, 0x63,
                0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
                0x00, 0x00, 0x00, 0x00, 0x01
            };

            private byte[] powerOffData = {
                0x68, 0x64, 0x00, 0x17, 0x64, 0x63,
                0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
                0x00, 0x00, 0x00, 0x00, 0x00
            };

            private byte[] tableData = {
                0x68, 0x64, 0x00, 0x1D, 0x72, 0x74,
                0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12,
                0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
            #endregion

            /// <summary>
            /// Default constructor
            /// </summary>
            /// <param name="address">IP address of socket</param>
            /// <param name="macAddress">MAC address of socket</param>
            /// <param name="port"></param>
            public Socket(IPAddress address, byte[] macAddress, int port)
            {
                //load in parameters
                ipAddress = address;
                this.macAddress = macAddress;
                this.port = port;
                name = string.Empty;
                isOn = false;
                subscribed = false;

                //write mac address into data (BE LE in sub)
                for (int i = 0; i < this.macAddress.Length; i++)
                {
                    discoverData[i + 6] = this.macAddress[i];

                    subData[i + 6] = this.macAddress[i];
                    subData[i + 18] = this.macAddress[this.macAddress.Length - i - 1];

                    powerOnData[i + 6] = this.macAddress[i];
                    powerOffData[i + 6] = this.macAddress[i];
                    tableData[i + 6] = this.macAddress[i];
                }
            }

            public void Discover()
            {
                UdpEngine(ipAddress, port, discoverData);
            }

            /// <summary>
            /// Subscribe to the socket
            /// </summary>
            public void Subscribe()
            {
                int nameLocation = 70;
                int nameMaxCharacters = 16;

                try
                {
                    UdpEngine(ipAddress, port, subData);

                    if (data.Length > 0 && data[data.Length - 1] == 0x0)
                    {
                        isOn = false;
                    }
                    else if (data.Length > 0 && data[data.Length - 1] == 0x1)
                    {
                        isOn = true;
                    }

                    //allow socket a nap
                    System.Threading.Thread.Sleep(150);

                    //get table four
                    tableData[22] = 0x4;
                    UdpEngine(ipAddress, port, tableData);
                    subscribedTime = DateTime.UtcNow;
                    subscribed = true;

                    //retrieve name
                    if (data.Length > nameLocation + nameMaxCharacters)
                    {
                        byte[] name = new byte[16];
                        for (int i = 0; i < nameMaxCharacters; i++)
                        {
                            name[i] = data[i + nameLocation];
                        }

                        //add it as string
                        this.name = Encoding.Default.GetString(name).TrimEnd();
                    }

                    //allow socket a nap
                    System.Threading.Thread.Sleep(150);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            /// Requests table data from subscribed socket
            /// </summary>
            /// <param name="tableId"></param>
            public void GetTable(byte tableId)
            {
                tableData[22] = tableId;

                UdpEngine(ipAddress, port, tableData);
            }

            /// <summary>
            /// Turn the socket off
            /// </summary>
            public void PowerOff()
            {
                UdpEngine(ipAddress, port, powerOffData);
            }

            /// <summary>
            /// Turn the socket on
            /// </summary>
            public void PowerOn()
            {
                UdpEngine(ipAddress, port, powerOnData);
            }

            /// <summary>
            /// Sends and recieves datagrams
            /// </summary>
            /// <param name="ipAddress">IP Address of target</param>
            /// <param name="port">Port of target</param>
            /// <param name="sendBytes">Data to send to target</param>
            private void UdpEngine(IPAddress ipAddress, int port, byte[] sendBytes)
            {
                UdpClient udpClient = new UdpClient(port);
                udpClient.Client.SendTimeout = 500;
                udpClient.Client.ReceiveTimeout = 500;

                try
                {
                    udpClient.Connect(ipAddress, port);
                    udpClient.Send(sendBytes, sendBytes.Length);

                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    byte[] data = receiveBytes;

                    udpClient.Close();
                    this.data = data;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    udpClient.Close();
                }
            }
        }

        /// <summary>
        /// Userland socket information
        /// </summary>
        public class SocketUI
        {
            public string name;
            public DateTime subscribedTime;
            public bool isOn;
            public string macAddress;
            public string ipAddress;
        }

        /// <summary>
        /// Populates the Socket class
        /// </summary>
        /// <param name="sockets">Client sockets</param>
        /// <param name="socket">Server socket</param>
        private static void BuildSocket(List<SocketUI> sockets, Socket socket)
        {
            SocketUI socketUi = new SocketUI();

            socketUi.ipAddress = socket.ipAddress.ToString();

            string macAddress = string.Empty;
            for (int i = 0; i < socket.macAddress.Length; i++)
            {
                macAddress += (int)socket.macAddress[i] + "-";
            }
            socketUi.macAddress = macAddress.Remove(macAddress.Length - 1);
            socketUi.subscribedTime = socket.subscribedTime;
            socketUi.name = socket.name;
            socketUi.isOn = socket.isOn;
            sockets.Add(socketUi);
        }

        /// <summary>
        /// Retrieves sockets
        /// </summary>
        /// <returns>List of sockets</returns>
        [HttpPost]
        public List<SocketUI> GetSockets(DateTime? subscribedTime)
        {
            //three min lease (missing 1 min from JS client)
            int subscriptionLease = 2;
            int attempts = 3;

            List<SocketUI> socketUis = new List<SocketUI>();

            if (subscribedTime == null || DateTime.UtcNow - subscribedTime >= TimeSpan.FromMinutes(subscriptionLease))
            {
                List<Socket> sockets = new List<Socket>();

                Socket socket1 = new Socket(IPAddress.Parse("192.168.0.8"), new byte[] { 0xac, 0xcf, 0x23, 0x8d, 0xa9, 0x12 }, 10000);
                Socket socket2 = new Socket(IPAddress.Parse("192.168.0.9"), new byte[] { 0xac, 0xcf, 0x23, 0x8d, 0x34, 0xcc }, 10000);
                Socket socket3 = new Socket(IPAddress.Parse("192.168.0.10"), new byte[] { 0xac, 0xcf, 0x23, 0x8d, 0xac, 0x12 }, 10000);

                sockets.Add(socket1);
                sockets.Add(socket2);
                sockets.Add(socket3);

                //try n times
                for (int i = 0; i < sockets.Count; i++)
                {
                    for (int j = 0; j < attempts; j++)
                    {
                        sockets[i].Subscribe();
                        if (sockets[i].subscribed)
                        {
                            BuildSocket(socketUis, sockets[i]);
                            break;
                        }
                    }
                }
            }

            return socketUis;
        }

        /// <summary>
        /// Sends out global discovery data
        /// </summary>
        /// <returns>True or false</returns>
        [HttpPost]
        public bool DiscoverGlobal()
        {
            byte[] discoverGlobalData = {
                0x68, 0x64, 0x00, 0x06, 0x71, 0x61
            };

            UdpClient udpClient = new UdpClient(10000);
            udpClient.Client.SendTimeout = 500;
            udpClient.Client.ReceiveTimeout = 500;

            try
            {
                udpClient.Connect(IPAddress.Parse("255.255.255.255"), 10000);
                udpClient.Send(discoverGlobalData, discoverGlobalData.Length);

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                byte[] data = receiveBytes;

                udpClient.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                udpClient.Close();
            }

            return true;
        }

        /// <summary>
        /// Toggles the socket on or off
        /// </summary>
        /// <param name="socketUi">Client object</param>
        /// <returns>True or false</returns>
        [HttpPost]
        public bool Switch(SocketUI socketUi)
        {
            //rebuild MAC byte array
            string[] macString = socketUi.macAddress.Split('-');
            byte[] macBytes = new byte[macString.Length];
            for (int i = 0; i < 6; i++)
            {
                macBytes[i] = Convert.ToByte(macString[i]);
            }

            Socket socket = new Socket(IPAddress.Parse(socketUi.ipAddress), macBytes, 10000);

            //toggle
            if (socketUi.isOn)
            {
                try
                {
                    socket.PowerOff();
                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                    return true;
                }
            }
            else
            {
                try
                {
                    socket.PowerOn();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                    return false;
                }
            }
        }

        /// <summary>
        /// Switches all sockets off
        /// </summary>
        /// <returns>True if any sockets were switched</returns>
        [HttpGet]
        public bool AllOff()
        {
            List<SocketUI> socketsUi = GetSockets(null);
            List<string> socketsOn = new List<string>();
            bool atLeastOne = false;

            for (int i = 0; i < socketsUi.Count; i++)
            {
                if (socketsUi[i].isOn)
                {
                    Switch(socketsUi[i]);
                    atLeastOne = true;
                    socketsOn.Add(socketsUi[i].name);
                }
            }

            //send shaming email
            if (socketsOn.Count > 0)
            {
                string body = string.Empty;

                if (socketsOn.Count == 1)
                {
                    body = socketsOn[0] + " has been switched off. Did you leave only one on for a reason?";
                }
                else if (socketsOn.Count == 2)
                {
                    body = socketsOn[0] + " and " + socketsOn[1] + " have been switched off. Hope that is ok.";
                }
                else
                {
                    var count = 0;
                    for (int i = 0; i < socketsOn.Count - 2; i++)
                    {
                        body = socketsOn[i] + ", ";
                        count++;
                    }

                    body += socketsOn[count] + " and " + socketsOn[count + 1] + " have been switched off. That's all the heaters.";
                }
            }

            return atLeastOne;
        }

        /// <summary>
        /// Switches all sockets on
        /// </summary>
        /// <returns>True if any sockets were switched</returns>
        [HttpGet]
        public bool AllOn()
        {
            List<SocketUI> socketsUi = GetSockets(null);
            bool atLeastOne = false;

            for (int i = 0; i < socketsUi.Count; i++)
            {
                if (!socketsUi[i].isOn)
                {
                    Switch(socketsUi[i]);
                    atLeastOne = true;
                }
            }

            return atLeastOne;
        }
    }
}