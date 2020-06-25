using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;

// UDP y Multicast.
using System.Net.Sockets;
using System.Text;

// IMAGE
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client_UDPMulticast
{
    public partial class Client : Form
    {
        private UdpClient udpclient = new UdpClient();

        private IPAddress multicastaddress = IPAddress.Parse("224.1.0.1");

        private IPEndPoint remoteep = new IPEndPoint(IPAddress.Any, 4672);
        private IPEndPoint remoteepChat;

        private MemoryStream ms;

        private byte[] payload;
        private byte[] time = new byte[4];
        private int tiempo;
        private byte[] time_rx = new byte[4];
        private byte type;
        private byte packetcounter;
        private int[] packetCounter = new int[255];
        private int perdidos = 0;
        private byte[] buffer;
        private int timestamp;
        private int timestampchat;
        private byte[] timechat;
        private byte[] rtp;
        private byte[] bufferchat_aux;
        private Byte[] bufferchat;
        private byte counter_chat = 0;
        private String ChatText;
        private Boolean RecibirVideo=false;

        public Client()
        {
            CheckForIllegalCrossThreadCalls = false;
            udpclient.JoinMulticastGroup(multicastaddress);
            remoteepChat = new IPEndPoint(multicastaddress, 8080);
            udpclient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            udpclient.Client.Bind(remoteep);
            InitializeComponent();
            Console.ReadLine();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Task t1 = new Task(visualizar_imagen);
            t1.Start();
            ChatText = "Esperando mensajes";
            SetChatText();
        }

        private void SetChatText()
        {
            Mensajes.Items.Add(ChatText);
        }

        private void visualizar_imagen()
        {
            while (true)
            {
                try
                {
                    buffer = udpclient.Receive(ref remoteep);
                    type = buffer[0]; //este es el byte que va a indicar el id del flujo
                    packetcounter = buffer[1]; //este es el byte que va a llevar la cuenta de los paquetes

                    if (type == 0)
                    {
                        if (packetcounter > 0 && packetcounter < 255)
                        {
                            packetCounter[packetcounter] = packetcounter;
                            int resta = (packetCounter[packetcounter] - packetCounter[packetcounter - 1]);

                            Console.WriteLine("---");
                            Console.WriteLine(packetCounter[packetcounter]);
                            Console.WriteLine(packetcounter);
                            if (resta != 1)
                            {
                                perdidos = resta;
                                Console.WriteLine("Se han perdido " + perdidos + " paquetes");
                            }
                        }
                    }

                    time_rx[0] = buffer[2];
                    time_rx[1] = buffer[3];
                    tiempo = BitConverter.ToInt32(time_rx, 0);
                    timestamp = DateTime.UtcNow.Millisecond;
                    if (tiempo < timestamp)
                    {
                        if (type == 0)
                        {
                            Console.WriteLine("Retardo vídeo = " + ((timestamp) - tiempo) + " ms");
                        }

                        if (type == 1)
                        {
                            Console.WriteLine("Retardo chat = " + ((timestamp) - tiempo) + " ms");
                        }
                    }

                    payload = new byte[buffer.Length - 6];

                    for (int i = 0; i < buffer.Length - 6; i++)
                    {
                        payload[i] = buffer[i + 6];
                    }

                    switch (type)
                    {
                        case 0: //tipo video
                            if (RecibirVideo == true)
                            {
                                ms = new MemoryStream(payload);
                                pictureBox1.Image = Image.FromStream(ms);
                            }
                            break;

                        case 1: //tipo texto
                            ChatText = Encoding.ASCII.GetString(payload, 0, payload.Length);
                            this.SetChatText();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RecibirVideo = true;
        }

        //Chat
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                rtp = new byte[2];
                byte[] msg = Encoding.UTF8.GetBytes(textBox1.Text + ": " + richTextBox1.Text);
                rtp[0] = 1; //decimos que el tag del chat es 1
                rtp[1] = counter_chat++;

                timestampchat = DateTime.UtcNow.Millisecond;
                timechat = BitConverter.GetBytes(timestampchat);

                bufferchat_aux = rtp.Concat(timechat).ToArray();
                bufferchat = new byte[bufferchat_aux.Length + msg.Length];
                bufferchat = bufferchat_aux.Concat(msg).ToArray();
                udpclient.Send(bufferchat, bufferchat.Length, remoteepChat);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            ChatText = textBox1.Text + ": " + richTextBox1.Text;
            richTextBox1.Clear();
            SetChatText();
        }
    }
}