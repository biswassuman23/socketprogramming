using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace ClientSockets.Synchronous.UsingByteArray // SyncScoketClient_ByteArr_Dll
{
    /// <summary>
    /// Client open a syncronous socket and send file to server, here asynchronous mode becomes by 
    /// Threading, and here uses Byte Array to transfer data.     
    /// </summary>
    public class SynchronusClient_ByteArr
    {
        public delegate void FileSendCompletedDelegate();
        public event FileSendCompletedDelegate FileSendCompleted, FileReceiveCompleted;
        public static int progress = 0, portSend, portReceive;
        int bufferSize;
        public static string status = "", presentOperation = "", ipAddress, outPathDefault = "";
        private static string outPathUserSelected = "";
        string clientId = "";
        string fileNameWithPath;
        public static bool isSaveToDefaultPath = false;
        static int totalSentDataSlot;

        public SynchronusClient_ByteArr(string clientId, string ipAddress, int sendPort, int receivePort, string DefaultPath)
        {
            this.clientId = clientId;
            SynchronusClient_ByteArr.ipAddress = ipAddress;
            SynchronusClient_ByteArr.portSend = sendPort;
            SynchronusClient_ByteArr.portReceive = receivePort;
            SynchronusClient_ByteArr.outPathDefault = DefaultPath;
            this.bufferSize = 10 * 1024;
        }
        /// <summary>
        /// Conestructor of Client Object
        /// </summary>
        /// <param name="ipAddress">IP Address of Server</param>
        /// <param name="outPathDefault">Path to store receive file temporary basis.</param>
        public SynchronusClient_ByteArr(string clientId, string ipAddress, string DefaultPath)
        {
            this.clientId = clientId;
            outPathDefault = DefaultPath;
            SynchronusClient_ByteArr.ipAddress = ipAddress;
            SynchronusClient_ByteArr.portSend = 8080;
            SynchronusClient_ByteArr.portReceive = 8081;
            this.bufferSize = 10 * 1024;
        }
        Thread startFileTransferThread, startFileReceiveThread;
        public string SendFileToServer( string senderId, string receiverId)
        {

            string newFileName, onlyFileName, path;

            //Select a File to transfer
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK)
                return "Cancelled by user";
            string fileNameWithPath = ofd.FileName;
            fileNameWithPath = fileNameWithPath.Replace("\\", "/");
            onlyFileName = fileNameWithPath;
            path = "";

            while (onlyFileName.IndexOf("/") != -1)
            {
                path += onlyFileName.Substring(0, onlyFileName.IndexOf("/")) + "/";
                onlyFileName = onlyFileName.Substring(onlyFileName.IndexOf("/") + 1);
            }
            newFileName = senderId + "#" + onlyFileName;
            //File.Copy(fileNameWithPath, path + "/" + newFileName,true);
            //File.SetAttributes(path + "/" + newFileName, FileAttributes.Hidden);

            string bothName = fileNameWithPath + "?" + path + "/" + newFileName;
            startFileTransferThread = new Thread(this.SendFileToServerByThread);
            startFileTransferThread.Start(bothName);

            //NEW FILE NAME IS NOW IN IT'S OWN EXTENSION NEED TO CHANGE .sjb FORM
            //Only file name
            int k = newFileName.Length - 1;
            while (newFileName.Substring(k, 1) != ".")
            {
                k--;
            }
            newFileName = newFileName.Substring(0, k);
            newFileName = newFileName + ".sjb";

            return newFileName;
        }
        private void SendFileToServerByThread(object fileNameWithPathObj)
        {
            Socket server = null;
            BinaryReader bReader = null;
            string newFileName = "";
            try
            {
                //BUILD A COPY OF SENDING FILE
                string oldFileName, bothFileName;
                bothFileName = fileNameWithPathObj.ToString();
                oldFileName = bothFileName.Substring(0, bothFileName.IndexOf("?"));
                newFileName = bothFileName.Substring(bothFileName.IndexOf("?") + 1);

                string filePath = "", tempFileName = oldFileName;
                while (tempFileName.IndexOf("/") != -1)
                {
                    filePath += tempFileName.Substring(0, tempFileName.IndexOf("/") + 1);
                    tempFileName = tempFileName.Substring(tempFileName.IndexOf("/") + 1);
                }
                if (File.Exists(newFileName))
                    File.Delete(newFileName);

                File.Copy(oldFileName, newFileName, true);
                File.SetAttributes(newFileName, FileAttributes.Hidden);

                status = "";
                this.fileNameWithPath = newFileName;
                //presentOperation = "Data Compressing";
                //this.fileNameWithPath = ZipUnzip.LZO_Algorithm.LZO.Compress(this.fileNameWithPath);
                //File.SetAttributes(filePath + this.fileNameWithPath, FileAttributes.Hidden);

                if (this.fileNameWithPath.Length == 0)
                    return;

                this.fileNameWithPath = filePath + tempFileName;// this.fileNameWithPath;
                presentOperation = "Data Sending";
                IPAddress[] serverIp = Dns.GetHostAddresses(SynchronusClient_ByteArr.ipAddress);
                IPEndPoint ipEnd = new IPEndPoint(serverIp[0], SynchronusClient_ByteArr.portSend);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

                server.Connect(ipEnd);

                int sentDataSize;
                byte[] data;// = new byte[bufferSize];

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

                string onlyFileName;
                fileNameWithPath = fileNameWithPath.Replace("\\", "/");
                onlyFileName = fileNameWithPath;
                while (onlyFileName.IndexOf("/") != -1)
                    onlyFileName = onlyFileName.Substring(onlyFileName.IndexOf("/") + 1);


                //DATA FORMAT: [FILE SIZE LEN INFO[0-3]][FILE NAME LEN INFO[4-7]][FILE NAME DATA][FILE CONTENT]

                byte[] dataLenBytes = BitConverter.GetBytes((int)bReader.BaseStream.Length);
                byte[] fileNameLenBytes = BitConverter.GetBytes(onlyFileName.Length);
                byte[] fileNameBytes = Encoding.ASCII.GetBytes(onlyFileName);

                byte[] allData = new byte[8 + fileNameBytes.Length + data.Length];

                //MAKE FIRST PACKET TO SERVER WITH FILE SIZE & FILE NAME
                dataLenBytes.CopyTo(allData, 0);
                fileNameLenBytes.CopyTo(allData, 4);
                fileNameBytes.CopyTo(allData, 8);
                data.CopyTo(allData, 8 + fileNameBytes.Length);

                //SEND FIRST PACKET TO SERVER
                server.Send(allData);
                totalSentDataSlot = 1;

                int totalDataSize = (int)bReader.BaseStream.Length;
                progress = (int)(((float)sentDataSize / totalDataSize) * 100);

                while (sentDataSize < bReader.BaseStream.Length)
                {
                    if (sentDataSize + bufferSize <= bReader.BaseStream.Length)
                    {
                        bReader.Read(data, 0, bufferSize);
                        sentDataSize += bufferSize;
                        progress = (int)(((float)sentDataSize / totalDataSize) * 100);
                        server.Send(data);
                    }
                    else
                    {
                        //SEND LAST PACKET
                        byte[] endData = new byte[(int)bReader.BaseStream.Length - sentDataSize];
                        bReader.Read(endData, 0, (int)bReader.BaseStream.Length - sentDataSize);
                        sentDataSize = (int)bReader.BaseStream.Length;
                        progress = (int)(((float)sentDataSize / totalDataSize) * 100);
                        server.Send(endData);

                    }
                    totalSentDataSlot++;
                }
                bReader.Close();

                File.GetAccessControl(fileNameWithPath);
                File.Delete(fileNameWithPath);

                File.GetAccessControl(newFileName);
                File.Delete(newFileName);

                byte[] receiveData = new byte[256];
                int recvLen = server.Receive(receiveData);
                server.Shutdown(SocketShutdown.Both);
                server.Close();
                if (Encoding.ASCII.GetString(receiveData, 0, recvLen) == "SUCCESS")
                {
                    presentOperation = "Data Sending Completed";
                    FileSendCompleted();
                }
            }
            #region SOCKET EXCEPTION
            catch (SocketException ex)
            {
                Console.WriteLine(ex.Message);
                if (bReader != null)
                    bReader.Close();
                if (server != null)
                    server.Close();
                if (File.Exists(fileNameWithPath))
                    File.Delete(fileNameWithPath);

                if (File.Exists(newFileName))
                    File.Delete(newFileName);
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
            catch (IOException ex)
            {
                status = ex.Message;
                if(bReader!=null)
                    bReader.Close();
                if (File.Exists(fileNameWithPath))
                    File.Delete(fileNameWithPath);

                if (File.Exists(newFileName))
                    File.Delete(newFileName);
            }
            return;
        }

        public void ReceiveFileFromServer(string onlyFileName)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                outPathUserSelected = fbd.SelectedPath;
                startFileReceiveThread = new Thread(this.ReceiveFileFromServerByThread);
                startFileReceiveThread.Start(onlyFileName);
            }
        }
        public void ReceiveFileFromServer(string onlyFileName, string ReceivePath)
        {
            outPathUserSelected = ReceivePath;
            startFileReceiveThread = new Thread(this.ReceiveFileFromServerByThread);
            startFileReceiveThread.Start(onlyFileName);
        }
        private void ReceiveFileFromServerByThread(object onlyFileNameObj)
        {

            Socket server = null;
            BinaryWriter bWriter = null; ;
            string rcvFileName = "", fileName = "";
            try
            {

                status = "";
                string onlyFileName = onlyFileNameObj.ToString();
                //presentOperation = "Data Compressing";
                //[CLIENT ID LENGTH][FILE NAME LENGTH][CLIENT ID][FILE NAME]
                //[2 BYTES][2 BYTES][... BYTES][... BYTES]

                if (clientId.Length == 0)
                    clientId = DateTime.Now.Ticks.ToString();
                byte[] clientIdLenBytes = BitConverter.GetBytes(clientId.Length);
                byte[] clientIdBytes = Encoding.ASCII.GetBytes(clientId);

                byte[] fileNameLenBytes = BitConverter.GetBytes(onlyFileName.Length);
                byte[] fileNameBytes = Encoding.ASCII.GetBytes(onlyFileName);

                byte[] allData = new byte[8 + fileNameBytes.Length + clientIdBytes.Length];

                clientIdLenBytes.CopyTo(allData, 0);
                fileNameLenBytes.CopyTo(allData, 4);

                clientIdBytes.CopyTo(allData, 8);
                fileNameBytes.CopyTo(allData, 8 + clientIdBytes.Length);


                presentOperation = "Data Sending";
                IPAddress[] serverIp = Dns.GetHostAddresses(SynchronusClient_ByteArr.ipAddress);
                IPEndPoint ipEnd = new IPEndPoint(serverIp[0], SynchronusClient_ByteArr.portReceive);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                server.Connect(ipEnd);
                server.Send(allData);
                //--------------------------------------------------------------------
                //NOW SERVER WILL SEND CLIENT'S FILE
                SynchronusClient_ByteArr.status = "";
                SynchronusClient_ByteArr.progress = 0;
                SynchronusClient_ByteArr.presentOperation = "";
                //server = (Socket)clientObject;
                bool flag = true;
                Console.WriteLine("New connection estublished. Socket {0}", server.GetHashCode());
                int totalDataLen, receivedLen, fileNameLen, fileContentStartIndex;

                byte[] data = new byte[bufferSize];
                //DATA FORMAT: [FILE SIZE LEN INFO[0-3]][FILE NAME LEN INFO[4-7]][FILE NAME DATA][FILE CONTENT]

                //GET FILE NAME, SIZE ETC.
                presentOperation = "Data Receiving";
                int len = server.Receive(data);
                if (len == 5)
                {
                    string strTemp = Encoding.ASCII.GetString(data, 0, len);
                    if (strTemp == "ERROR")
                    {
                        status = "File yet not sent by Sender";
                        presentOperation = "Data Receiving Not Started";
                        progress = 0;
                        return;
                    }
                }

                totalDataLen = BitConverter.ToInt32(data, 0);
                fileNameLen = BitConverter.ToInt32(data, 4);
                fileName = Encoding.ASCII.GetString(data, 8, fileNameLen);
                fileContentStartIndex = 4 + 4 + fileNameLen;
                receivedLen = len;// -fileContentStartIndex;

                //READ DATA & STORE OF FIRST PACKET
                outPathDefault = outPathDefault.Replace("\\", "/");
                if (outPathDefault.Substring(outPathDefault.Length - 2) != "/")
                    outPathDefault += "/";

                outPathUserSelected = outPathUserSelected.Replace("\\", "/");
                if (outPathUserSelected.Substring(outPathUserSelected.Length - 2) != "/")
                    outPathUserSelected += "/";
                //FolderBrowserDialog fbd = new FolderBrowserDialog();
                //if (fbd.ShowDialog() == DialogResult.OK)
                {
                    if (isSaveToDefaultPath)
                    {
                        if (File.Exists(outPathDefault + fileName))
                            File.Delete(outPathDefault + fileName);
                    }
                    else
                    {
                        if (File.Exists(outPathUserSelected + fileName))
                            File.Delete(outPathUserSelected + fileName);
                    }
                    
                    if (isSaveToDefaultPath)
                    {
                        bWriter = new BinaryWriter(File.Open(outPathDefault + fileName, FileMode.Append));
                        File.SetAttributes(outPathDefault + fileName, FileAttributes.Hidden);
                    }
                    else
                    {
                        bWriter = new BinaryWriter(File.Open(outPathUserSelected + fileName, FileMode.Append));
                        //File.SetAttributes(outPathUserSelected + fileName, FileAttributes.Hidden);
                    }
                    //BinaryWriter bWriter = new BinaryWriter(File.Open(fbd.SelectedPath +"/"+ fileName, FileMode.Append));

                    bWriter.Write(data, fileContentStartIndex, receivedLen - fileContentStartIndex);

                    progress = (int)(((float)receivedLen / (float)totalDataLen) * 100);

                    while (true)
                    {
                        if (receivedLen < totalDataLen)
                        {
                            //READ & SAVE REMINING DATA
                            len = server.Receive(data);
                            bWriter.Write(data, 0, len);

                            receivedLen += len;
                            float val = (float)receivedLen / (float)totalDataLen;
                            progress = (int)(((float)receivedLen / (float)totalDataLen) * 100);
                            Console.WriteLine("Data received {0}", receivedLen);
                        }
                        else
                        {
                            //NO MORE DATA CLOSE CONNECTION AFTER ACK OF CLIENT
                            bWriter.Close();
                            Console.WriteLine("Client data sending completed.\n\n");
                            server.Close();
                            //presentOperation = "Data Decompressing";

                            //if (isSaveToDefaultPath)
                            //    rcvFileName = ZipUnzip.LZO_Algorithm.LZO.Decompress(outPathDefault + fileName, outPathDefault);
                           // else
                             //   rcvFileName = ZipUnzip.LZO_Algorithm.LZO.Decompress(outPathUserSelected + fileName, outPathUserSelected);

                            string orgFileName = rcvFileName=fileName;
                            while (orgFileName.IndexOf("#") != -1)
                                orgFileName = orgFileName.Substring(orgFileName.IndexOf("#") + 1);

                            if (isSaveToDefaultPath)
                            {
                                File.Copy(outPathDefault + rcvFileName, outPathDefault + orgFileName, true);
                                File.Delete(outPathDefault + rcvFileName);
                                File.GetAccessControl(outPathDefault + fileName);
                                File.Delete(outPathDefault + fileName);
                            }
                            else
                            {

                                //File.Copy(outPathUserSelected + rcvFileName, outPathUserSelected + orgFileName, true);
                                //File.Delete(outPathUserSelected + rcvFileName);
                                File.GetAccessControl(outPathUserSelected + fileName);
                                //File.Delete(outPathUserSelected + fileName);
                            }
                            //SynchronousSocketServer.isServerRunning = false;
                            presentOperation = "Data Receiving Completed";
                            FileReceiveCompleted();
                            progress = 100;
                            return;
                        }
                    }
                }
                server.Close();


            }
            #region SOCKET EXCEPTION
            catch (SocketException ex)
            {
                if (bWriter != null)
                    bWriter.Close();

                if (isSaveToDefaultPath)
                {
                    if (File.Exists(outPathDefault + rcvFileName))
                        File.Delete(outPathDefault + rcvFileName);
                    if (File.Exists(outPathDefault + fileName))
                        File.Delete(outPathDefault + fileName);
                }
                else
                {
                    if (File.Exists(outPathUserSelected + rcvFileName))
                        File.Delete(outPathUserSelected + rcvFileName);

                    if (File.Exists(outPathUserSelected + fileName))
                        File.Delete(outPathUserSelected + fileName);
                }
                Console.WriteLine(ex.Message);
                if (server != null)
                    server.Close();
                
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
            catch (IOException ex)
            {
                status = ex.Message;
                if (bWriter != null)
                    bWriter.Close();

                if (isSaveToDefaultPath)
                {
                    if (File.Exists(outPathDefault + rcvFileName))
                        File.Delete(outPathDefault + rcvFileName);
                    if (File.Exists(outPathDefault + fileName))
                        File.Delete(outPathDefault + fileName);
                }
                else
                {
                    if (File.Exists(outPathUserSelected + rcvFileName))
                        File.Delete(outPathUserSelected + rcvFileName);

                    if (File.Exists(outPathUserSelected + fileName))
                        File.Delete(outPathUserSelected + fileName);
                }
            }
        }
    }
}

