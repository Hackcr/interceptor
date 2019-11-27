﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading;

namespace socketserver
{
    class Client
    {
        private byte[] EncodeToUTF8(string line)
        {
            Encoding utf8 = Encoding.GetEncoding("UTF-8");
            Encoding win1251 = Encoding.GetEncoding("Windows-1251");

            byte[] win1251Bytes = win1251.GetBytes(line);

            return Encoding.Convert(win1251, utf8, win1251Bytes);
        }

        private void SendResponse(TcpClient Client, string line)
        {
            byte[] Buffer = EncodeToUTF8(line);

            Console.WriteLine("отправлен ответ: " + line);

            Client.GetStream().Write(Buffer, 0, Buffer.Length);

            Client.Close();
        }

        public Client(TcpClient Client, string clientIP)
        {
            string request = String.Empty;

            byte[] buffer = new byte[1024];
            int count = 0;

            while ((count = Client.GetStream().Read(buffer, 0, buffer.Length)) > 0)
            {
                request += Encoding.ASCII.GetString(buffer, 0, count);

                if (request.IndexOf("\r\n\r\n") >= 0)
                    break;
            }

            request = Uri.UnescapeDataString(request);

            Match ReqMatch = Regex.Match(request, @"to=([\d]+)&message=([^;]+?);");

            string cleanRequest = ReqMatch.Groups[2].Value;
            string toCashbox = ReqMatch.Groups[1].Value;

            Console.WriteLine(" ");
            Console.WriteLine("---> " + cleanRequest);

            if (!Program.Sockets2Send[toCashbox].Connected)
                return;

            Program.Sockets2Send[toCashbox].Send(Encoding.Unicode.GetBytes(cleanRequest));

            byte[] data = new byte[256];
            StringBuilder builder = new StringBuilder();

            do
            {
                builder.Append(Encoding.Unicode.GetString(data, 0, Program.Sockets2Send[toCashbox].Receive(data, data.Length, 0)));
            }
            while (Program.Sockets2Send[toCashbox].Available > 0);

            SendResponse(Client, builder.ToString());

            Console.WriteLine("<--- " + builder.ToString());

            Client.Close();
        }
    }
}
