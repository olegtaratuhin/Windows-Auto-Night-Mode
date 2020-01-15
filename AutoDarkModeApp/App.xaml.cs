﻿using AutoDarkModeApp.Communication;
using AutoDarkModeApp.Config;
using AutoDarkModeSvc.Handler;
using AutoDarkMode;
using NetMQ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoDarkModeApp
{
    public partial class App : Application
    {
        private readonly AutoDarkModeConfigBuilder autoDarkModeConfigBuilder = AutoDarkModeConfigBuilder.Instance();
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            bool isClassicMode;
            try
            {
                isClassicMode = autoDarkModeConfigBuilder.Config.ClassicMode;
            }
            catch (Exception)
            {
                isClassicMode = false;
            }

            List<string> args;
            if (e.Args.Length > 0)
            {
                args = new List<string>(e.Args);
            }
            else
            {
                args = new List<string>();
            }


            using Process svc = new Process();
            if (e.Args.Length == 0 || e.Args.Length > 0 && e.Args[0] != "/debug")
            {
                svc.StartInfo.UseShellExecute = false;
                svc.StartInfo.FileName = Path.Combine(Extensions.ExecutionDir, "AutoDarkModeSvc.exe");
                svc.StartInfo.CreateNoWindow = true;
                svc.Start();
            }
            else
            {
                args.Remove("/debug");
            }

            //handle command line arguments
            if (args.Count > 0)
            {
                ICommandClient commandClient = new ZeroMQClient(PipeMessage.DefaultPort);
                foreach (var value in args)
                {
                    if (value == "/switch")
                    {
                        commandClient.SendMessage(value);
                    }
                    else if (value == "/swap")
                    {
                        commandClient.SendMessage(value);
                    }
                    else if (value == "/dark")
                    {
                        commandClient.SendMessage(value);
                    }
                    else if (value == "/light")
                    {
                        commandClient.SendMessage(value);
                    }
                    else if (value == "/update")
                    {
                        Updater updater = new Updater();
                        updater.CheckNewVersion();
                    }
                    else if (value == "/location")
                    {
                        LocationHandler locationHandler = new LocationHandler();
                        await locationHandler.SetLocationSilent();
                    }
                    else if (value == "/removeTask")
                    {
                        TaskSchdHandler.RemoveTask();
                    }
                    else if (value == "/removeAutostart")
                    {
                        RegeditHandler regEditHandler = new RegeditHandler();
                        regEditHandler.RemoveAutoStart();
                    }
                    else if (value == "/pipeclienttest")
                    {
                        //ICommandClient pc = new PipeClient(Tools.DefaultPipeName);
                        commandClient.SendMessage(PipeMessage.TestError);
                    }

                    if (isClassicMode) commandClient.SendMessage(PipeMessage.Shutdown);
                    NetMQConfig.Cleanup();
                    Shutdown();
                }
            }
            else
            {
                MainWindow mainWin = new MainWindow();
                mainWin.Show();
            }
        }
    }
}
