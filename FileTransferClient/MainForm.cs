using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils;
namespace FileTransferGUI
{
    public partial class MainForm : Form
    {
        ClientWebSocket webSocket;
        RSACrypto rsa;
        public MainForm()
        {
            InitializeComponent();
        }

        private void closeWebSocket()
        {

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                webSocket.CloseAsync(WebSocketCloseStatus.Empty, null, new CancellationToken()).Wait();
                webSocket.Dispose();
            }
        }
        private void send(string msg, bool final = true)
        {
            send(Encoding.Default.GetBytes(msg), final);
        }

        private void send(byte[] msg, bool final = true)
        {
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                ArraySegment<byte> data = new ArraySegment<byte>(msg);
                webSocket.SendAsync(data, WebSocketMessageType.Text, final, new CancellationToken()).Wait();
                ArraySegment<byte> result = new ArraySegment<byte>(new byte[65535]);
                MemoryStream memory = new MemoryStream();
                int length = 0;
                while (true)
                {
                    Task<WebSocketReceiveResult> ret = webSocket.ReceiveAsync(result, new CancellationToken());
                    ret.Wait();
                    memory.Write(result.Array, 0, ret.Result.Count);
                    length += ret.Result.Count;
                    if (ret.Result.EndOfMessage)
                    {
                        break;
                    }
                }
                memory.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                memory.Read(buffer, 0, length);
                memory.Close();
                memory.Dispose();
                if (length > 0)
                {
                    ParseResponse(buffer);
                }
            }
        }

        private void download(string file)
        {
            List<string> param = new List<string>();
            param.Add(file);
            Operation op = new Operation()
            {
                op = "download",
                param = param
            };
            send(JsonConvert.SerializeObject(op));
        }
        private void reqAuth()
        {
            List<string> param = new List<string>();
            param.Add(File.ReadAllText("pub.key"));
            Operation op = new Operation()
            {
                op = "reqauth",
                param = param
            };
            send(JsonConvert.SerializeObject(op));


        }
        private void listFile()
        {
            Operation op = new Operation()
            {
                op = "list",
                param = null
            };
            send(JsonConvert.SerializeObject(op));


        }
        private void ParseResponse(byte[] result)
        {
            string message = Encoding.Default.GetString(result);

            Operation msg = JsonConvert.DeserializeObject<Operation>(message);
            switch (msg.op)
            {
                case "auth":
                    rsa = new RSACrypto(File.ReadAllText("pri.key"), true);
                    break;
                case "list_file":
                    lvFileBrowser.Items.Clear();
                    int id = 1;
                    foreach (string i in msg.param)
                    {
                        ListViewItem tmp = new ListViewItem(new string[] { id.ToString(), i, "Not Download" });
                        lvFileBrowser.Items.Add(tmp);
                        id++;
                    }
                    break;
                case "send_file":
                    string path = "data/" + msg.param[0];
                    File.WriteAllBytes(path, rsa.DecodeOrNull(RSA_Unit.Base64DecodeBytes(msg.param[1])));
                    if (HashTool.SHA256File(new FileStream(path, FileMode.Open)) == msg.param[2])
                    {
                        lvFileBrowser.SelectedItems[0].SubItems[2].Text = "Verified";
                    }
                    else
                    {
                        lvFileBrowser.SelectedItems[0].SubItems[2].Text = "Error";
                    }
                    break;
                default:
                    break;
            }

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (btnConnect.Text == "Connect")
            {
                webSocket = new ClientWebSocket();
                Uri uri = new Uri(string.Format("ws://{0}:{1}/", tbIPAddress.Text, tbPort.Text));
                webSocket.ConnectAsync(uri, new CancellationToken()).Wait();
                reqAuth();
                btnConnect.Text = "DisConnect";
                gbFileBrowser.Enabled = true;
            }
            else
            {
                closeWebSocket();
                btnConnect.Text = "Connect";
                gbFileBrowser.Enabled = false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeWebSocket();
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (lvFileBrowser.SelectedIndices != null)
            {
                download(lvFileBrowser.SelectedItems[0].SubItems[1].Text);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            listFile();
        }
    }
}
