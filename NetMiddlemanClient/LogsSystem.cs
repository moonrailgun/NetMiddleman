﻿using System;
using System.IO;

namespace NetMiddlemanClient
{
    class LogsSystem
    {
        #region 单例模式
        private static LogsSystem _instance;
        public static LogsSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LogsSystem();
                }
                return _instance;
            }
        }
        #endregion

        private string logDate;
        private string logPath;
        private string logFileName;

        public LogsSystem()
        {
            SetLogFileInfo();
        }

        /// <summary>
        /// 设置文件IO的信息
        /// logDate:日期
        /// logPath:文件夹地址
        /// logFileName:日志文件完整地址
        /// </summary>
        private void SetLogFileInfo()
        {
            try
            {
                logDate = DateTime.Now.ToString("yyyy-MM-dd");
                logPath = Environment.CurrentDirectory + "/Logs/";
                logFileName = logPath + logDate + ".log";
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 用于跨天数的日志记录更改
        /// 每次调用文件时先调用该函数检查一遍日期是否更换
        /// </summary>
        private void CheckLogFileInfo()
        {
            if (logDate != DateTime.Now.ToString("yyyy-MM-dd"))
            {
                SetLogFileInfo();//重新设置文件信息
            }
        }

        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="mainLog">日志主体内容</param>
        /// <param name="level">日志等级</param>
        public void Print(string mainLog, LogLevel level = LogLevel.INFO)
        {
            CheckLogFileInfo();//检查是否已经更换日期了
            try
            {
                string log = string.Format("[{0} {1}] : {2}", DateTime.Now.ToString("HH:mm:ss"), level.ToString(), mainLog);

                //写入数据
                FileStream fs = new FileStream(logFileName, FileMode.Append);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(log);
                sw.Close();
                fs.Close();
            }
            catch (IOException)
            {
            }
            catch (Exception)
            {
            }
        }
    }

    public enum LogLevel
    {
        INFO = 0, WARN = 1, ERROR = 2
    }
}
