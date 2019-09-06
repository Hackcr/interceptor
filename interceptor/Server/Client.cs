﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading;

namespace interceptor
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
            Log.Add("отправлен ответ: " + line);

            byte[] Buffer = EncodeToUTF8(line);

            Client.GetStream().Write(Buffer, 0, Buffer.Length);

            Client.Close();
        }

        public Client(TcpClient Client, string clientIP)
        {
            string request = String.Empty;
            string response = String.Empty;

            byte[] buffer = new byte[1024];
            int count = 0;
            
            while ((count = Client.GetStream().Read(buffer, 0, buffer.Length)) > 0)
            {
                request += Encoding.ASCII.GetString(buffer, 0, count);

                if (request.IndexOf("\r\n\r\n") >= 0)
                    break;
            }

            request = Uri.UnescapeDataString(request);

            Log.Add(request, logType: "http");

            Match ReqMatch = Regex.Match(request, @"message=([^;]+?);");

            bool emplyRequest = (ReqMatch.Success ? false : true);

            response = ResponsePrepare(ReqMatch.Groups[1].Value, emplyRequest, clientIP);

            SendResponse(Client, response);

            Log.Add("соединение закрыто");

            Client.Close();
        }

        public static void ShowTotal(string summ)
        {
            Application.Current.Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                MainWindow main = (MainWindow)Application.Current.MainWindow;
                main.total.Content = "сумма: " + summ;
                main.totalR.Content = main.total.Content;
                main.totalContent.Visibility = Visibility.Visible;
                main.totalRContent.Visibility = Visibility.Visible;
            }));
        }

        public static string ResponsePrepare(string request, bool empty, string clientIP)
        {
            string err = String.Empty;

            if (empty)
            {
                Log.Add("пустой запрос с " + clientIP);
                CRM.SendError("Пустой запрос с " + clientIP);

                return "403";
            }
            else if (!CheckRequest.CheckValidRequest(request))
            {
                Log.Add("невалидный запрос " + clientIP);
                CRM.SendError("Невалидный запрос с " + clientIP);

                return "404";
            }

            if (!CheckRequest.CheckLoginInRequest(request, out err))
            {
                Log.Add("конфликт логинов: " + err);
                CRM.SendError("Конфликт логинов: " + err);

                return "ERR3:Кассовая программа запущена пользователем " + CRM.currentLogin;
            }

            if (CheckRequest.CheckConnection(request))
            {
                Log.Add("бип-тест подключения к кассе");

                string testResult = Diagnostics.MakeBeepTest();

                return testResult;
            }

            if (!CheckRequest.CheckXml(request))
            {
                Log.Add("CRC ошибочна, возвращаем ошибку данных");
                CRM.SendError("CRC ошибка");

                return "ERR1:Ошибка переданных данных";
            }
            else
            {
                Log.Add("CRC запроса корректна, логин соответствует");

                DocPack docPack = new DocPack();

                docPack.DocPackFromXML(request);

                if (!String.IsNullOrWhiteSpace(docPack.AgrNumber))
                    Log.Add("номер договора: " + docPack.AgrNumber);

                MainWindow.Cashbox.manDocPackForPrinting = docPack;

                if (docPack.RequestOnly == 1)
                {
                    MainWindow.Cashbox.manDocPackSumm = docPack.Total;

                    ShowTotal(docPack.Total.ToString());

                    return "OK:Callback запрос получен";
                } 
                else
                    return MainWindow.Cashbox.PrintDocPack(docPack);
            }
                
        }
    }
}
