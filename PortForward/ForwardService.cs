using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PortForward
{
    public class ForwardService
    {
        static bool canRun = false;

        static int ExtPort = 8076;
        static int LocalPort = 8075;
        public void Start()
        {
            var ExtPortStr = System.Configuration.ConfigurationManager.AppSettings["ExtPort"];
            if (!string.IsNullOrEmpty(ExtPortStr))
            {
                if (!int.TryParse(ExtPortStr, out ExtPort))
                {
                    Log.Logger.ExceptionLog(new ApplicationException("外部端口配置不正确:" + ExtPortStr));
                }
            }

            var LocalPortStr = System.Configuration.ConfigurationManager.AppSettings["LocalPort"];
            if (!string.IsNullOrEmpty(LocalPortStr))
            {
                if (!int.TryParse(LocalPortStr, out LocalPort))
                {
                    Log.Logger.ExceptionLog(new ApplicationException("外部端口配置不正确:" + LocalPortStr));
                }
            }

            canRun = true;
            Listening(ExtPort);

        }


        public void Stop()
        {
            canRun = false;
        }


        /// <summary>
        /// 新开线程，监听。
        /// 外部负责关闭返回的TcpListener
        /// </summary>
        /// <param name="port"></param>
        /// <param name="onConnected"></param>
        /// <returns></returns>
        public static TcpListener Listening(int port)
        {
            var listener = new TcpListener(new System.Net.IPEndPoint(0, port));
            listener.Start();
            try
            {
                new Task(() =>
                {
                    try
                    {
                        while (canRun)
                        {
                            var client = listener.AcceptTcpClient();
                            new Task(() =>
                            {
                                try
                                {
                                    OnInputConnected(client);
                                }
                                catch (Exception e)
                                {
                                    try
                                    {
                                        Log.Logger.ExceptionLog(e);
                                    }
                                    catch { }
                                }
                            }).Start();
                        }
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            Log.Logger.ExceptionLog(e);
                        }
                        catch { }
                    }
                }).Start();
            }
            catch (Exception e)
            {
                try
                {
                    listener.Stop();
                }
                catch { }

                try
                {
                    Log.Logger.ExceptionLog(e);
                }
                catch { }
            }
            return listener;
        }


        static void OnInputConnected(TcpClient inputClient)
        {
            //Bridge
            string RemoteEndPoint = null;
            try
            {
                RemoteEndPoint = inputClient.Client.RemoteEndPoint.ToString();
                var outputClient = new TcpClient();
                outputClient.Connect(IPAddress.Parse("127.0.0.1"), LocalPort);

                if (Bridge(inputClient, outputClient))
                {
                    Log.Logger.TraceLog(DateTime.Now.ToString("[HH:mm:ss.fff]") + "转发成功[" + RemoteEndPoint + "]");
                    Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss.fff]") + "转发成功[" + RemoteEndPoint + "]");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.TraceLog(DateTime.Now.ToString("[HH:mm:ss.fff]") + "转发失败[" + RemoteEndPoint + "]:" + ex.GetBaseException().Message);
                Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss.fff]") + "转发失败[" + RemoteEndPoint + "]:" + ex.GetBaseException().Message);
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientA"></param>
        /// <param name="clientB"></param>
        /// <returns></returns>
        public static bool Bridge(TcpClient clientA, TcpClient clientB)
        {
            if (!clientA.Connected || !clientB.Connected)
            {
                clientA.Close();
                clientB.Close();
                return false;
            }

            //桥接
            #region get data from clientA  to clientB
            new Task(() =>
            {
                try
                {
                    using (NetworkStream reader = clientA.GetStream())
                    using (NetworkStream writer = clientB.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int size;

                        while (canRun)
                        {
                            size = reader.Read(buffer, 0, buffer.Length);
                            if (size > 0) writer.Write(buffer, 0, size);
                            else
                                if (!TcpClientIsConnected(clientA))
                                break;
                        }
                    }
                }
                catch { }

                try
                {
                    clientA.Close();
                }
                catch { }

                try
                {
                    clientB.Close();
                }
                catch { }

            }).Start();
            #endregion


            #region get data from clientB  to clientA
            new Task(() =>
            {
                try
                {
                    using (NetworkStream reader = clientB.GetStream())
                    using (NetworkStream writer = clientA.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int size;
                        while (canRun)
                        {
                            size = reader.Read(buffer, 0, buffer.Length);
                            if (size > 0) writer.Write(buffer, 0, size);
                            else
                                if (!TcpClientIsConnected(clientB))
                                break;
                        }
                    }
                }
                catch { }

                try
                {
                    clientA.Close();
                }
                catch { }

                try
                {
                    clientB.Close();
                }
                catch { }

            }).Start();
            #endregion


            return true;
        }

        public static bool TcpClientIsConnected(TcpClient c, int microSeconds = 500)
        {
            return null != c && c.Client.Connected && !(c.Client.Poll(microSeconds, SelectMode.SelectRead) && (c.Client.Available == 0));
        }

    }
}
