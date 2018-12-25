using Fleck;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Utils;

namespace FileTransferServer
{
    public partial class ServerForm : Form
    {
        WebSocketServer server = new WebSocketServer("ws://0.0.0.0:7181");
        List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();
        RSACrypto rsa;
        public ServerForm()
        {
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    var data = socket.ConnectionInfo; //通过data可以获得这个链接传递过来的Cookie信息，用来区分各个链接和用户之间的关系（如果需要后台主动推送信息到某个客户的时候，可以使用Cookie）
                    Log(socket, "Opened");
                    allSockets.Add(socket);
                };

                socket.OnClose = () =>// 当关闭Socket链接十执行此方法
                {
                    Log(socket, "Closed");
                    allSockets.Remove(socket);
                };

                socket.OnMessage = message =>// 接收客户端发送过来的信息
                {
                    try { 
                    Log(socket, message);
                    Operation msg = JsonConvert.DeserializeObject<Operation>(message);
                    switch (msg.op)
                    {
                        case "reqauth":
                            rsa = new RSACrypto(msg.param[0], true);
                            auth(socket);
                            break;
                        case "list":
                            listFiles(socket);
                            break;
                        case "download":
                            sendFile(socket, msg.param[0]);
                            break;
                        default:
                            Log(socket, "Unknown Message: " + message);
                            break;
                    }
                    }catch(Exception ex)
                    {
                        Log(socket, ex.ToString());
                    }
                    //allSockets.ToList().ForEach(s => s.Send("Echo: " + message));
                };
                socket.OnError = ex =>
                {
                    Log(socket, ex.ToString());
                };
            });
            InitializeComponent();
        }
        private void listFiles(IWebSocketConnection webSocket)
        {
            List<string> param = new List<string>();
            foreach (ListViewItem listViewItem in lvFiles.Items)
            {
                if (listViewItem.Checked)
                {
                    param.Add(listViewItem.Text);
                }
            }
            Operation op = new Operation()
            {
                op = "list_file",
                param = param
            };
            send(webSocket, JsonConvert.SerializeObject(op));
        }

        private void sendFile(IWebSocketConnection webSocket, string filename)
        {
            if (!File.Exists(filename))
            {
                Log(webSocket, filename + " not exists");
                return;
            }
            List<string> param = new List<string>();
            param.Add(Path.GetFileName(filename));
            param.Add(RSA_Unit.Base64EncodeBytes(rsa.Encode(File.ReadAllBytes(filename))));
            param.Add(HashTool.SHA256File(new FileStream(filename, FileMode.Open)));
            Operation op = new Operation()
            {
                op = "send_file",
                param = param
            };
            Log(string.Format("{0} {1}", param[0], param[2]));
            send(webSocket, JsonConvert.SerializeObject(op));
        }
        private void auth(IWebSocketConnection webSocket)
        {
            Operation op = new Operation()
            {
                op = "auth",
                param = null
            };
            send(webSocket, JsonConvert.SerializeObject(op));
        }

        private void send(IWebSocketConnection webSocket, string msg, bool final = true)
        {
            send(webSocket, Encoding.Default.GetBytes(msg), final);
        }

        private void send(IWebSocketConnection webSocket, byte[] msg, bool final = true)
        {
            if (webSocket != null && webSocket.IsAvailable == true)
            {
                webSocket.Send(msg);
            }
        }

        public void Log(IWebSocketConnection isocket, string msg)
        {
            Log(isocket.ConnectionInfo.ClientIpAddress + ":" + isocket.ConnectionInfo.ClientPort + " " + msg);
        }
        public void Log(string msg)
        {
            tbLog.AppendText(string.Format("【{0}】 {1}\r\n", DateTime.Now.ToString(), msg));
        }

        private void lvFiles_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (string i in s)
            {
                if (!lvFiles.Items.ContainsKey(i) && File.Exists(i))
                {

                    ListViewItem listViewItem = lvFiles.Items.Add(i, i, 0);
                    listViewItem.Checked = true;
                }
            }
        }

        private void lvFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void lvFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                foreach (ListViewItem item in lvFiles.SelectedItems)
                {
                    lvFiles.Items.Remove(item);
                }
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in lvFiles.Items)
                {
                    item.Selected = true;
                }
            }
            else if (e.KeyCode == Keys.Space)
            {
                foreach (ListViewItem item in lvFiles.SelectedItems)
                {
                    item.Checked = true;
                }
            }
        }
    }
}
