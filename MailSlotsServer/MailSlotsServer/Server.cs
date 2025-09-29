using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

namespace MailSlots
{
    public partial class frmMain : Form
    {
        private int ClientHandleMailSlot;       // дескриптор мэйлслота
        private string MailSlotName = "\\\\" + Dns.GetHostName() + "\\mailslot\\ServerMailslot";    // имя мэйлслота, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private Thread t;                       // поток для обслуживания мэйлслота
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом
        private List<string> clients = new List<string>();


        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            // создание мэйлслота
            ClientHandleMailSlot = DIS.Import.CreateMailslot("\\\\.\\mailslot\\ServerMailslot", 0, DIS.Types.MAILSLOT_WAIT_FOREVER, 0);

            this.Text += ClientHandleMailSlot.ToString();

            // вывод имени мэйлслота в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + MailSlotName;

            // создание потока, отвечающего за работу с мэйлслотом
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        public uint SendToPipe(string message, string pipe)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(
                message
                );    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            // открываем именованный канал, имя которого указано в поле tbPipe
            Int32 PipeHandle = DIS.Import.CreateFile(pipe, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(PipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал
            DIS.Import.CloseHandle(PipeHandle);
            return BytesWritten;
        }
        private string pipesToText(List<string> clients)
        {
            return String.Join("\n",
                                    (new List<string> { "participants" }).Concat(
                                        clients.Select(
                                            x => {
                                                string[] splitted = x.Split(new string[] { "\\" }, StringSplitOptions.None);
                                                return splitted[splitted.Count()-1];
                                            }
                                            ).ToArray()
                                        ).ToArray());
        }


        private void ReceiveMessage()
        {
            string msg = "";            // прочитанное сообщение
            int MailslotSize = 0;       // максимальный размер сообщения
            int lpNextSize = 0;         // размер следующего сообщения
            int MessageCount = 0;       // количество сообщений в мэйлслоте
            uint realBytesReaded = 0;   // количество реально прочитанных из мэйлслота байтов

            // входим в бесконечный цикл работы с мэйлслотом
            while (_continue)
            {
                // получаем информацию о состоянии мэйлслота
               bool gotInfo = DIS.Import.GetMailslotInfo(ClientHandleMailSlot, MailslotSize, ref lpNextSize, ref MessageCount, 0);
                if (!gotInfo)
                    Console.WriteLine("err get info");

                // если есть сообщения в мэйлслоте, то обрабатываем каждое из них
                if (MessageCount > 0)
                    for (int i = 0; i < MessageCount; i++)
                    {
                        byte[] buff = new byte[1024];                           // буфер прочитанных из мэйлслота байтов
                        DIS.Import.FlushFileBuffers(ClientHandleMailSlot);      // "принудительная" запись данных, расположенные в буфере операционной системы, в файл мэйлслота
                        DIS.Import.ReadFile(ClientHandleMailSlot, buff, 1024, ref realBytesReaded, 0);      // считываем последовательность байтов из мэйлслота в буфер buff
                        msg = Encoding.Unicode.GetString(buff);                 // выполняем преобразование байтов в последовательность символов
                        
                        rtbMessages.Invoke((MethodInvoker)delegate
                        {
                            if (msg != "")
                            {
                                string[] data = msg.Split(new string[] { " <:> " }, StringSplitOptions.None);
                                string clientpipename = "\\\\"+data[0]+"\\pipe\\"+data[1];
                                if (!clients.Contains(clientpipename))
                                {
                                    clients.Add(clientpipename);
                                    rtbParticipants.Text = pipesToText(clients);
                                }
                                DateTime dt = DateTime.Now;
                                string time = dt.Hour + ":" + dt.Minute+":"+dt.Second;

                                string message = "\n >> "  + data[1] + "|" + data[0] + "|" + time  + ":  " + data[2];                             // выводим полученное сообщение на форму
                                rtbMessages.Text += message;
                                List<string> delete = new List<string>();
                                foreach (string pipe in clients)
                                {
                                    Console.WriteLine(message+" "+pipe);
                                    if (SendToPipe(message, pipe) == 0)
                                    {
                                        delete.Add(pipe);
                                    }
                                }
                                foreach (var pipe in delete)
                                {
                                    clients.Remove(pipe);
                                    rtbParticipants.Text = pipesToText(clients);
                                }
                            }
                        });
                        Thread.Sleep(500);                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                    }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с мэйлслотом завершена

            if (t != null)
                t.Abort();          // завершаем поток

            if (ClientHandleMailSlot != -1)
                DIS.Import.CloseHandle(ClientHandleMailSlot);            // закрываем дескриптор мэйлслота
        }
    }
}