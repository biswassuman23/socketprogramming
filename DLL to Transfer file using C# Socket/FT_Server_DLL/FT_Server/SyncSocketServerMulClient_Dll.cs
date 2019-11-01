using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace ServerSockets.Synchronous.UsingByteArray
{
    /// <summary>
    /// That server code will run to access multiple client requrest and response. Here will not any 
    /// Un-zip and Decript technology. Server will save all data in Encripted-Zipped form which will
    /// sent by Client.
    /// </summary>
    public class SyncSocketServerMulClient
    {
        public static bool isServerRunning=false;
        public static int receivePort,sendPort, maxClientReceived, bufferSize;
        //public static int  progress=0 ;
        public string outPath ;
        public static string status="",presentOperation = "";
        public string currentStatus = "";
        

        public SyncSocketServerMulClient(int receivePort,int sendPort,int maxClient, string outPutPath)
        {
            SyncSocketServerMulClient.receivePort = receivePort;
            SyncSocketServerMulClient.sendPort = sendPort;
            SyncSocketServerMulClient.bufferSize = 10 * 1024;
            SyncSocketServerMulClient.maxClientReceived = maxClient;

            outPutPath = outPutPath.Replace("\\", "/");
            if (outPutPath.Substring(outPutPath.Length - 1) != "/")
                outPutPath += "/";

            this.outPath = outPutPath;
            SyncSocketServerMulClient.status = "";
            //SyncSocketServerMulClient.progress = 0;
            SyncSocketServerMulClient.presentOperation = "";
        }
        /// <summary>
        /// Default sets Receive Port:8080, Send Port:8081, Buffer Size:10KB, Max Client:100 and Out path: C:\
        /// </summary>
        public SyncSocketServerMulClient()
        {
            SyncSocketServerMulClient.receivePort = 8080;
            SyncSocketServerMulClient.sendPort = 8081;
            SyncSocketServerMulClient.bufferSize = 10 * 1024;
            SyncSocketServerMulClient.maxClientReceived = 100;
            this.outPath = "c:/";
            SyncSocketServerMulClient.status = "";
            //SyncSocketServerMulClient.progress = 0;
            SyncSocketServerMulClient.presentOperation = "";
            
        }
        #region SERVER SYNCHRONOUS SOCKET DATA RECEIVE
        Thread threadReceiveServer, threadSendServer;
        private void StartReceiveServer()
        {
            SyncSocketServerMulClient.isServerRunning = true;
            threadReceiveServer = new Thread(new ThreadStart(this.StartReceiveServerThread));
            threadReceiveServer.Start();
        }
        private void StopReceiveServer()
        {
            SyncSocketServerMulClient.isServerRunning = false;
            //
            try
            {

                IPAddress[] ipAddress = Dns.GetHostAddresses("localhost");
                IPEndPoint ipEnd = new IPEndPoint(ipAddress[0], SyncSocketServerMulClient.receivePort);
                Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                clientSock.Connect(ipEnd);

                string strData = "END";
                byte[] clientData = new byte[30];
                clientData = Encoding.ASCII.GetBytes(strData);
                clientSock.Send(clientData);

                //byte[] serverData = new byte[10];
                //int len = clientSock.Receive(serverData);
                //Console.WriteLine(Encoding.ASCII.GetString(serverData, 0, len));
                clientSock.Close();

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
            //
        }

        Socket receiveSock,sendSock;
        IPEndPoint ipEndReceive, ipEndSend;
        private void StartReceiveServerThread()
        {
            ipEndReceive = new IPEndPoint(IPAddress.Any, SyncSocketServerMulClient.receivePort);
            receiveSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            receiveSock.Bind(ipEndReceive);
            SyncSocketServerMulClient.isServerRunning = true;
            receiveSock.Listen(maxClientReceived);
            Console.WriteLine("Waiting for new client connection");
            while (SyncSocketServerMulClient.isServerRunning)
            {
                Socket clientSock;
                clientSock = receiveSock.Accept();

                SyncSocketServerMulClient serObj = new SyncSocketServerMulClient(SyncSocketServerMulClient.receivePort, SyncSocketServerMulClient.sendPort, SyncSocketServerMulClient.bufferSize, this.outPath);
                Thread newClient = new Thread(serObj.ReadDataFromClient);
                newClient.Start(clientSock);
            }
            receiveSock.Close();

        }
        private void ReadDataFromClient(object clientObject)
        {
            Socket clientSock = null;
            BinaryWriter bWriter=null;
            string fileName = "";
            try
            {
                SyncSocketServerMulClient.status = "";
                SyncSocketServerMulClient.presentOperation = "";
                clientSock = (Socket)clientObject;
                bool flag = true;
                Console.WriteLine("New connection estublished. Socket {0}", clientSock.GetHashCode());
                int totalDataLen, receivedLen, fileNameLen, fileContentStartIndex;

                byte[] data = new byte[bufferSize];
                //DATA FORMAT: [FILE SIZE LEN INFO[0-3]][FILE NAME LEN INFO[4-7]][FILE NAME DATA][FILE CONTENT]



                //GET FILE NAME, SIZE ETC.
                currentStatus =presentOperation = "Data Receiving";
                int len = clientSock.Receive(data);
                if (len == 0)
                {
                    clientSock.Close();
                    return;
                }
                if (len == 3)
                {
                    string clientData = Encoding.ASCII.GetString(data);
                    if (clientData.Substring(0, len) == "END")
                        return;
                }
                totalDataLen = BitConverter.ToInt32(data, 0);
                fileNameLen = BitConverter.ToInt32(data, 4);
                fileName = Encoding.ASCII.GetString(data, 8, fileNameLen);
                fileContentStartIndex = 4 + 4 + fileNameLen;
                receivedLen = len - fileContentStartIndex;
                //READ DATA & STORE OF FIRST PACKET

                //DELETE IF FILE ALREADY EXIST
                if (File.Exists(outPath + fileName))
                    File.Delete(outPath + fileName);

                bWriter = new BinaryWriter(File.Open(outPath + fileName, FileMode.Append));
                bWriter.Write(data, 0, len);
                while (true)
                {
                    if (receivedLen < totalDataLen)
                    {
                        //READ & SAVE REMINING DATA
                        len = clientSock.Receive(data);
                        bWriter.Write(data, 0, len);

                        receivedLen += len;
                        Console.WriteLine("Data received {0}", receivedLen);
                    }
                    else
                    {
                        //NO MORE DATA CLOSE CONNECTION AFTER ACK OF CLIENT
                        bWriter.Close();
                        Console.WriteLine("Client data sending completed.\n\n");

                        //currentStatus =presentOperation = "Data Decompressing";
                        currentStatus = presentOperation = "Data Receiving Completed";


                        byte[] clientInfoData = new byte[256];
                        clientInfoData = Encoding.ASCII.GetBytes("SUCCESS");
                        clientSock.Send(clientInfoData);
                        clientSock.Close();
                        return;
                    }
                }
                clientSock.Close();
            }
            #region SOCKET EXCEPTION
            catch (SocketException ex)
            {
                if(bWriter!=null)
                    bWriter.Close();

                if (File.Exists(outPath + fileName))
                    File.Delete(outPath + fileName);

                Console.WriteLine(ex.Message);
                if (clientSock != null)
                    clientSock.Close();
                switch (ex.SocketErrorCode)
                {
                    case SocketError.AccessDenied:
                        status = "Access Denied by server end.";
                        break;

                    case SocketError.AddressAlreadyInUse:
                        status = "Address Already In Use.";
                        break;

                    case SocketError.AddressNotAvailable:
                        status = "Address Not Available.";
                        break;

                    case SocketError.ConnectionAborted:
                        status = "Connection aborted by server.";
                        break;

                    case SocketError.ConnectionRefused:
                        status = "Connection refused by server.";
                        break;

                    case SocketError.ConnectionReset:
                        status = "Connection Reset.";
                        break;

                    case SocketError.DestinationAddressRequired:
                        status = "Destination Address Required.";
                        break;

                    case SocketError.Disconnecting:
                        status = "Disconnecting.";
                        break;

                    case SocketError.HostDown:
                        status = "Target Host is Down.";
                        break;

                    case SocketError.HostNotFound:
                        status = "Target Host Not Found.";
                        break;

                    case SocketError.HostUnreachable:
                        status = "Target Host Unreachable.";
                        break;

                    case SocketError.InProgress:
                        status = "In Progress.";
                        break;

                    case SocketError.Interrupted:
                        status = "Interrupted.";
                        break;

                    case SocketError.InvalidArgument:
                        status = "Invalid Argument.";
                        break;

                    case SocketError.NetworkDown:
                        status = "Network Down.";
                        break;

                    case SocketError.NetworkReset:
                        status = "Network Reset.";
                        break;

                    case SocketError.NetworkUnreachable:
                        status = "Network Unreachable.";
                        break;

                    case SocketError.NoBufferSpaceAvailable:
                        status = "No Buffer Space Available.";
                        break;

                    case SocketError.NoData:
                        status = "No Data.";
                        break;

                    case SocketError.NotConnected:
                        status = "Not Connected.";
                        break;

                    case SocketError.NotInitialized:
                        status = "Not Initialized.";
                        break;

                    case SocketError.NotSocket:
                        status = "Not Socket.";
                        break;

                    case SocketError.OperationAborted:
                        status = "Operation Aborted.";
                        break;

                    case SocketError.Shutdown:
                        status = "Socket already Shutdown.";
                        break;

                    case SocketError.TooManyOpenSockets:
                        status = "Too Many Open Sockets.";
                        break;

                    case SocketError.WouldBlock:
                        status = "Too Many Open Sockets.";
                        break;

                    default:
                        status = "Have some unknown problem in Socket.";
                        break;
                }
            }
            #endregion
            catch (Exception err)
            {
                if (bWriter != null)
                    bWriter.Close();

                if (File.Exists(outPath + fileName))
                    File.Delete(outPath + fileName);
            }
        }
        #endregion
        #region SERVER SYNCHRONOUS SOCKET DATA SEND
        private void StartSendServer()
        {
            SyncSocketServerMulClient.isServerRunning = true;
            threadSendServer = new Thread(new ThreadStart(this.StartSendServerThread));
            threadSendServer.Start();
        }
        private void StartSendServerThread()
        {
            ipEndSend = new IPEndPoint(IPAddress.Any, SyncSocketServerMulClient.sendPort);
            sendSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sendSock.Bind(ipEndSend);
            SyncSocketServerMulClient.isServerRunning = true;
            sendSock.Listen(maxClientReceived);
            Console.WriteLine("Waiting for new client connection");
            while (SyncSocketServerMulClient.isServerRunning)
            {
                Socket clientSock;
                clientSock = sendSock.Accept();

                SyncSocketServerMulClient serObj = new SyncSocketServerMulClient(SyncSocketServerMulClient.receivePort, SyncSocketServerMulClient.sendPort, SyncSocketServerMulClient.bufferSize, this.outPath);
                Thread newClient = new Thread(serObj.SendDataToClient);
                newClient.Start(clientSock);
            }
            sendSock.Close();
        }
        private void SendDataToClient(object clientObject)
        {
            Socket clientSocket=(Socket)clientObject;
            string fileNameWithPath = "";
            BinaryReader bReader = null;
            try
            {
                status = "";
                

                byte[] clientData = new byte[1024];
                int clientDataLen = clientSocket.Receive(clientData);
                if (clientDataLen == 0)
                {
                    clientSocket.Close();
                    return;
                }

                if (clientDataLen == 3)
                {
                    string clientDataStr = Encoding.ASCII.GetString(clientData);
                    if (clientDataStr.Substring(0, clientDataLen) == "END")
                        return;
                }

                //GET USER ID & REQUESTED FILE NAME
                //[CLIENT ID LENGTH][FILE NAME LENGTH][CLIENT ID][FILE NAME]
                //[2 BYTES][2 BYTES][... BYTES][... BYTES]
                //Based on Client Id, file path will be determined.
                int clientIdLen, fileNameLen;
                string clientId, fileName;
                clientIdLen = BitConverter.ToInt32(clientData, 0);
                fileNameLen= BitConverter.ToInt32(clientData, 4);

                
                clientId = Encoding.ASCII.GetString(clientData, 8, clientIdLen);
                fileName = Encoding.ASCII.GetString(clientData, 8+clientIdLen, fileNameLen);

                string copiedFileName = clientId + "." + fileName;//ONE COPY OF TARGET FILE WILL BE CREATED AND START
                //RECEIVING FROM THERE, AFTER RECEIVE COMPLETION THIS FILE WILL BE DELETED BY CLIENT SOCKET THREAD
                
                //GET FILE PATH BASED ON CLIENT ID HERE
                fileNameWithPath = this.outPath;

                //UPDATE FILE PATH WITH "/" ENDING IF "/" NOT EXIST
                fileNameWithPath = fileNameWithPath.Replace("\\", "/");
                if (fileNameWithPath.Substring(fileNameWithPath.Length - 1) != "/")
                    fileNameWithPath += "/";

                //MAKE ONE COPY OF REQUESTED FILE FOR CURRENT CLIENT
                if (File.Exists(fileNameWithPath + copiedFileName))
                    File.Delete(fileNameWithPath + copiedFileName);
                File.Copy(fileNameWithPath +fileName, fileNameWithPath +copiedFileName);
                fileNameWithPath += copiedFileName;

                //GET FILE PATH BASED ON CLIENT ID HERE
                if (fileNameWithPath.Length == 0)
                    return;

                currentStatus = presentOperation = "Data Sending";
                int sentDataSize;
                byte[] data;// = new byte[bufferSize];

                //CHECK IF FILE NAME EXISTS OR NOT
                if (File.Exists(fileNameWithPath))
                {
                    bReader = new BinaryReader(File.Open(fileNameWithPath, FileMode.Open));
                    if (bReader.BaseStream.Length <= bufferSize)
                    {
                        data = new byte[bReader.BaseStream.Length];
                        bReader.Read(data, 0, (int)bReader.BaseStream.Length);
                        sentDataSize = (int)bReader.BaseStream.Length;
                    }
                    else
                    {
                        data = new byte[bufferSize];
                        bReader.Read(data, 0, bufferSize);
                        sentDataSize = bufferSize;
                    }

                    //SEND FIRST PACKET TO SERVER
                    clientSocket.Send(data);
                    
                    int totalSentDataSlot = 1;

                    int totalDataSize = (int)bReader.BaseStream.Length;
                    //progress = (int)(((float)sentDataSize / totalDataSize) * 100);

                    while (sentDataSize < bReader.BaseStream.Length)
                    {
                        if (sentDataSize + bufferSize <= bReader.BaseStream.Length)
                        {
                            bReader.Read(data, 0, bufferSize);
                            sentDataSize += bufferSize;
                            //progress = (int)(((float)sentDataSize / totalDataSize) * 100);
                            clientSocket.Send(data);
                        }
                        else
                        {
                            //SEND LAST PACKET
                            byte[] endData = new byte[(int)bReader.BaseStream.Length - sentDataSize];
                            bReader.Read(endData, 0, (int)bReader.BaseStream.Length - sentDataSize);
                            sentDataSize = (int)bReader.BaseStream.Length;
                            //progress = (int)(((float)sentDataSize / totalDataSize) * 100);
                            clientSocket.Send(endData);

                        }
                        totalSentDataSlot++;
                    }
                    bReader.Close();
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    File.Delete(fileNameWithPath);
                    currentStatus = presentOperation = "Data Sending Completed";
                }
                else//FILE NOT FOUND
                {
                    string strFleErr = "ERROR";// ;
                    byte[] tempFleErrArr = Encoding.ASCII.GetBytes(strFleErr);
                    clientSocket.Send(tempFleErrArr);
                    currentStatus = presentOperation = "Client " + strFleErr + ": File yet not sent by Sender";
                }

            }
            #region SOCKET EXCEPTION
            catch (SocketException ex)
            {
                Console.WriteLine(ex.Message);
                if (bReader != null)
                    bReader.Close();
                if (clientSocket != null)
                    clientSocket.Close();
                switch (ex.SocketErrorCode)
                {
                    case SocketError.AccessDenied:
                        status = "Access Denied by server end.";
                        break;

                    case SocketError.AddressAlreadyInUse:
                        status = "Address Already In Use.";
                        break;

                    case SocketError.AddressNotAvailable:
                        status = "Address Not Available.";
                        break;

                    case SocketError.ConnectionAborted:
                        status = "Connection aborted by server.";
                        break;

                    case SocketError.ConnectionRefused:
                        status = "Connection refused by server.";
                        break;

                    case SocketError.ConnectionReset:
                        status = "Connection Reset.";
                        break;

                    case SocketError.DestinationAddressRequired:
                        status = "Destination Address Required.";
                        break;

                    case SocketError.Disconnecting:
                        status = "Disconnecting.";
                        break;

                    case SocketError.HostDown:
                        status = "Target Host is Down.";
                        break;

                    case SocketError.HostNotFound:
                        status = "Target Host Not Found.";
                        break;

                    case SocketError.HostUnreachable:
                        status = "Target Host Unreachable.";
                        break;

                    case SocketError.InProgress:
                        status = "In Progress.";
                        break;

                    case SocketError.Interrupted:
                        status = "Interrupted.";
                        break;

                    case SocketError.InvalidArgument:
                        status = "Invalid Argument.";
                        break;

                    case SocketError.NetworkDown:
                        status = "Network Down.";
                        break;

                    case SocketError.NetworkReset:
                        status = "Network Reset.";
                        break;

                    case SocketError.NetworkUnreachable:
                        status = "Network Unreachable.";
                        break;

                    case SocketError.NoBufferSpaceAvailable:
                        status = "No Buffer Space Available.";
                        break;

                    case SocketError.NoData:
                        status = "No Data.";
                        break;

                    case SocketError.NotConnected:
                        status = "Not Connected.";
                        break;

                    case SocketError.NotInitialized:
                        status = "Not Initialized.";
                        break;

                    case SocketError.NotSocket:
                        status = "Not Socket.";
                        break;

                    case SocketError.OperationAborted:
                        status = "Operation Aborted.";
                        break;

                    case SocketError.Shutdown:
                        status = "Socket already Shutdown.";
                        break;

                    case SocketError.TooManyOpenSockets:
                        status = "Too Many Open Sockets.";
                        break;

                    case SocketError.WouldBlock:
                        status = "Too Many Open Sockets.";
                        break;

                    default:
                        status = "Have some unknown problem in Socket.";
                        break;
                }
            #endregion
            }
            catch(IOException ex)
            {
                status = ex.Message;
                if (bReader != null)
                    bReader.Close();
                if (File.Exists(fileNameWithPath))
                    File.Delete(fileNameWithPath);
            }
                
                return;
            }
        private void StopSendServer()
        {
            SyncSocketServerMulClient.isServerRunning = false;
            //
            try
            {

                IPAddress[] ipAddress = Dns.GetHostAddresses("localhost");
                IPEndPoint ipEnd = new IPEndPoint(ipAddress[0], SyncSocketServerMulClient.sendPort);
                Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                clientSock.Connect(ipEnd);

                string strData = "END";
                byte[] clientData = new byte[30];
                clientData = Encoding.ASCII.GetBytes(strData);
                clientSock.Send(clientData);

                //byte[] serverData = new byte[10];
                //int len = clientSock.Receive(serverData);
                //Console.WriteLine(Encoding.ASCII.GetString(serverData, 0, len));
                clientSock.Close();

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
            //
        }
        #endregion


        public void StartServer()
        {
            StartReceiveServer();
            StartSendServer();
        }
        public void StopServer()
        {
            StopReceiveServer();
            StopSendServer();
        }
    }

}
