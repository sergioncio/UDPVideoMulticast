using System;
using System.Drawing;

// IMAGE
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;

// UDP y Multicast.
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Touchless.Vision.Camera;

namespace WebcamUDPMulticast
{
    public partial class Form1 : Form
    {
        private CameraFrameSource _frameSource;
        private static Bitmap _latestFrame;
        private UdpClient udpserver = new UdpClient(8080);

        private IPAddress multicastaddress = IPAddress.Parse("224.1.0.1");

        private IPEndPoint remote;
        private IPEndPoint remoteChat;
        private string ChatText;

        private MemoryStream ms;

        private byte[] rtp;
        private byte[] rtp_chat;
        private byte[] buffer_aux;
        private Byte[] buffer;
        private byte[] bufferchat_aux;
        private Byte[] bufferchat;
        private byte counter = 0;
        private byte counter_chat = 0;
        private byte[] time;
        private byte[] timechat;
        private String timestamp;
        private String timestampchat;
        private String timestampchat_tx;
        private int tiempo;
        private byte[] timechat_tx;
        private byte[] time_rx;
        private byte type;
        private byte[] payload;
        private int tiempochat;

        public Form1()
        {
            CheckForIllegalCrossThreadCalls = false;
            udpserver.JoinMulticastGroup(multicastaddress);
            InitializeComponent();
            Console.ReadLine();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxCameras.Items.Clear();
            foreach (Camera cam in CameraService.AvailableCameras)
            {
                comboBoxCameras.Items.Add(cam);
            }

            Thread t = new Thread(new ThreadStart(MyThreadMethod));
            t.Start();

            //Chat
            remoteChat = null;
            ChatText = "Esperando mensajes";
            SetChatText();
        }

        private void startCapturing()
        {
            try
            {
                /* Se seleccionada la cámara y se inicia la captura de imágenes.
                   Se establece parámetros como el tamaño de la imagen capturada o el número de imágenes por segundo. */
                Camera c = (Camera)comboBoxCameras.SelectedItem;
                setFrameSource(new CameraFrameSource(c));
                _frameSource.Camera.CaptureWidth = 1080;
                _frameSource.Camera.CaptureHeight = 720;
                _frameSource.Camera.Fps = 60;
                _frameSource.NewFrame += OnImageCaptured;
                //La imagen es visualizada a través del elemento pictureBoxDisplay
                pictureBox1.Paint += new PaintEventHandler(drawLatestImage);
                _frameSource.StartFrameCapture();
            }
            catch (Exception ex)
            {
                comboBoxCameras.Text = "Select a Camera";
                MessageBox.Show(ex.Message);
            }
        }

        public void OnImageCaptured(Touchless.Vision.Contracts.IFrameSource frameSource, Touchless.Vision.Contracts.Frame frame, double fps)
        {
            _latestFrame = frame.Image;
            pictureBox1.Invalidate();
        }

        private void drawLatestImage(object sender, PaintEventArgs e)
        {
            remote = new IPEndPoint(multicastaddress, 4672);
            if (_latestFrame != null)
            {   // Aquí se deberá redimensionar la imagen _latestFrame a el tamaño de la picturebox1
                e.Graphics.DrawImage(_latestFrame, pictureBox1.Width, 0, -pictureBox1.Width,
                pictureBox1.Height);
                // Aquí se insertaría el código para enviar imágenes
                ms = new MemoryStream();
                Bitmap bitmap = new Bitmap(_latestFrame, new Size(650, 390));
                bitmap.Save(ms, ImageFormat.Jpeg);
                rtp = new byte[2];
                rtp[0] = 0; //decimos que el tag del video es 0 
                rtp[1] = counter; //contador de control de paquetes  
                counter++;
                timestamp = DateTime.Now.ToString("yyyyMMddHHmmssff");
                time = Encoding.ASCII.GetBytes(timestamp);
                buffer_aux = new byte[rtp.Length + time.Length];
                buffer_aux = rtp.Concat(time).ToArray();
                buffer = new byte[buffer_aux.Length + ms.ToArray().Length];
                buffer = buffer_aux.Concat(ms.ToArray()).ToArray();
                udpserver.Send(buffer, buffer.Length, remote);
            }
        }

        private void SetChatText()
        {
            Mensajes.Items.Add(ChatText);
        }

        private void quitarCamara()
        {
            if (_frameSource != null)
            {
                _frameSource.NewFrame -= OnImageCaptured;
                _frameSource.Camera.Dispose();
                setFrameSource(null);
                pictureBox1.Paint += new PaintEventHandler(drawLatestImage);
            }
        }

        private void setFrameSource(CameraFrameSource cameraFrameSource)
        {
            if (_frameSource == cameraFrameSource)
                return;

            _frameSource = cameraFrameSource;
        }

        private void MyThreadMethod()
        {
            while (true)
            {
                Byte[] bytes = udpserver.Receive(ref remoteChat);
                type = buffer[0]; //este es el byte que va a indicar el id del flujo 
                time_rx[0] = buffer[2];
                time_rx[1] = buffer[3];
                tiempo = BitConverter.ToInt32(time_rx, 0);
                timestampchat = DateTime.Now.ToString("yyyyMMddHHmmssff");
                timechat = Encoding.ASCII.GetBytes(timestampchat);
                tiempochat = BitConverter.ToInt32(timechat, 0);
                if (tiempo < tiempochat)
                {
                    Console.WriteLine("Retardo: " + ((tiempochat) - tiempo));
                }

                payload = new byte[buffer.Length - 6];
                for (int i = 0; i < buffer.Length - 6; i++)
                {
                    payload[i] = buffer[i + 6];
                }
                if (type == 1)
                {
                    ChatText = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
                    this.SetChatText();
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            quitarCamara();
            startCapturing();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                rtp_chat = new byte[2];
                byte[] msg = Encoding.UTF8.GetBytes(textBox1.Text + ": " + richTextBox1.Text);
                bufferchat = new byte[rtp.Length + msg.Length];
                rtp_chat[0] = 1; 
                rtp_chat[1] = counter_chat++;

                timestampchat_tx = DateTime.Now.ToString("yyyyMMddHHmmssff");
                timechat_tx = Encoding.ASCII.GetBytes(timestamp);

                bufferchat_aux = rtp.Concat(time).ToArray();
                bufferchat = new byte[bufferchat_aux.Length + msg.Length];
                bufferchat = bufferchat_aux.Concat(msg).ToArray();
                udpserver.Send(bufferchat, bufferchat.Length, remote);
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