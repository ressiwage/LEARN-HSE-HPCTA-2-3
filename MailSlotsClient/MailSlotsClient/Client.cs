using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MailSlots
{
    public partial class frmMain : Form
    {
        private Int32 HandleMailSlot;   // дескриптор мэйлслота
        string nickname;

        private Int32 PipeHandle;   // дескриптор канала
        private Int32 NicknameNamedPipeHandle;
        private volatile bool _continue = true;

        private Thread t;
        public static bool IsBasicLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
        public static bool FormatValid(string format)
        {
            if (format.Length == 0) return false;
            foreach (char c in format)
            {
                // This is using String.Contains for .NET 2 compat.,
                //   hence the requirement for ToString()
                if (!IsBasicLetter(c))
                    return false;
            }

            return true;
        }
        public static String getPipeValidName()
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "введите никнейм",
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top=20, Text="введите никнейм", Width=400 };
            TextBox textBox = new TextBox() { Left = 50, Top=50, Width=400 };
            Button confirmation = new Button() { Text = "Ok", Left=350, Width=100, Top=70 };
            confirmation.Click += (sender, e) => {
                if (FormatValid(textBox.Text))
                {
                    prompt.Close();
                }
                else
                {
                    textLabel.Text = "Некорректный ввод. Никнейм должен содержать только латинские буквы";
                    textLabel.ForeColor = Color.Red;
                }
            };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            String nickname = textBox.Text == "" ? "anon" : textBox.Text;
            return nickname;
        }

        private void ReceiveMessage()
        {
            string msg = "";            // прочитанное сообщение
            uint realBytesReaded = 0;   // количество реально прочитанных из канала байтов

            // входим в бесконечный цикл работы с каналом
            while (_continue)
            {
                if (DIS.Import.ConnectNamedPipe(NicknameNamedPipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(NicknameNamedPipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(NicknameNamedPipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    msg = Encoding.Unicode.GetString(buff);                                 // выполняем преобразование байтов в последовательность символов
                    messagesTB.Invoke((MethodInvoker)delegate
                    {
                        if (msg != "")
                            messagesTB.Text += "\n >> " + msg;                             // выводим полученное сообщение на форму
                    });

                    DIS.Import.DisconnectNamedPipe(NicknameNamedPipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
        }
        // конструктор формы
        public frmMain()
        {
            this.nickname = getPipeValidName();
            InitializeComponent();
            nickBox.Text = "ваш ник: " + this.nickname;
            this.Text += "     " + Dns.GetHostName();   // выводим имя текущей машины в заголовок формы
            this.NicknameNamedPipeHandle = DIS.Import.CreateNamedPipe("\\\\.\\pipe\\"+this.nickname,
                DIS.Types.PIPE_ACCESS_DUPLEX,
                DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT,
                DIS.Types.PIPE_UNLIMITED_INSTANCES,
                0,
                1024,
                DIS.Types.NMPWAIT_WAIT_FOREVER,
                (uint)0);
            t = new Thread(ReceiveMessage);
            t.Start();
        }

        // присоединение к мэйлслоту
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // открываем мэйлслот, имя которого указано в поле tbMailSlot
                HandleMailSlot = DIS.Import.CreateFile(tbMailSlot.Text, DIS.Types.EFileAccess.GenericAll, DIS.Types.EFileShare.Read , 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
                if (HandleMailSlot != -1)
                {
                    btnConnect.Enabled = false;
                    btnSend.Enabled = true;
                }
                else
                    MessageBox.Show("Не удалось подключиться к мейлслоту");
            }
            catch
            {
                MessageBox.Show("Не удалось подключиться к мейлслоту");
            }
        }

        // отправка сообщения
        private void btnSend_Click(object sender, EventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт

            string dnsHostName = Dns.GetHostName().ToString();
            string nick = this.nickname;
            byte[] buff = Encoding.Unicode.GetBytes(
                dnsHostName + " <:> " + nick + " <:> " + tbMessage.Text.Replace(" <:> ", "") + " <:> "
                );
            //byte[] buff = Encoding.Unicode.GetBytes(Dns.GetHostName().ToString() + " >> " + tbMessage.Text);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            DIS.Import.WriteFile(HandleMailSlot, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            DIS.Import.CloseHandle(HandleMailSlot);     // закрываем дескриптор мэйлслота
        }
    }
}