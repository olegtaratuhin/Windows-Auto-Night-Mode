﻿// Copyright (c) 2022 namazso <admin@namazso.eu>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AutoDarkModeLib;
using AutoDarkModeSvc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static AutoDarkModeSvc.Handlers.IThemeManager2.Flags;

namespace AutoDarkModeSvc.Handlers.IThemeManager2
{

    public static class Tm2Handler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static GlobalState state = GlobalState.Instance();

        [DllImport("ole32.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int CoCreateInstance(
            [In, MarshalAs(UnmanagedType.LPStruct)]
            Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            [In, MarshalAs(UnmanagedType.LPStruct)]
            Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv
        );

        private static Interfaces.IThemeManager2 InitManager()
        {
            var hr = CoCreateInstance(
                       Guid.Parse("9324da94-50ec-4a14-a770-e90ca03e7c8f"),
                       IntPtr.Zero,
                       0x17,
                       typeof(Interfaces.IThemeManager2).GUID,
                       out var obj);
            if (obj == null)
            {
                throw new ExternalException($"cannot create IThemeManager2 instance: {hr:x8}!", hr);
            }
            var manager = (Interfaces.IThemeManager2)obj;
            manager.Init(InitializationFlags.ThemeInitNoFlags);
            return manager;
        }


        private static bool SetThemeViaIdx(Theme2Wrapper theme, Interfaces.IThemeManager2 manager)
        {

            int res = manager.SetCurrentTheme(IntPtr.Zero, theme.Idx, 1, 0, 0);
            if (res != 0)
            {
                throw new ExternalException($"error setting theme via id, hr: {res}", res);
            }
            return true;
        }

        private static bool SetThemeViaPath(string path, Interfaces.IThemeManager2 manager)
        {
            int res = manager.OpenTheme(IntPtr.Zero, path, ThemePackFlags.ThemepackFlagSilent);
            if (res != 0)
            {
                throw new ExternalException($"error setting theme via path, hr: {res}", res);
            }
            return true;
        }

        /// <summary>
        /// Sets a theme given a path
        /// </summary>
        /// <param name="path">the path of the theme file</param>
        /// <returns>the first tuple entry is true if the theme was found, the second is true if theme switching was successful</returns>
        public static (bool, bool) SetTheme(string displayName, string originalPath)
        {
            bool found = false;
            bool success = false;

            if (displayName == null)
            {
                return (found, success);
            }

            Thread thread = new(() =>
            {
                try
                {
                    var manager = InitManager();

                    if (state.LearnedThemeNames.ContainsKey(displayName))
                    {
                        Logger.Debug($"using learned theme name: {displayName}={state.LearnedThemeNames[displayName]}");
                        displayName = state.LearnedThemeNames[displayName];
                    }

                    List<Theme2Wrapper> themes = GetThemeList(manager);

                    if (themes.Count > 0)
                    {
                        Theme2Wrapper targetTheme = themes.Where(t => t.ThemeName == displayName).FirstOrDefault();
                        if (targetTheme != null)
                        {
                            found = true;
                            success = SetThemeViaIdx(targetTheme, manager);
                            if (success)
                            {
                                Logger.Info($"applied theme {targetTheme.ThemeName}, from origin: {originalPath} directly via IThemeManager2");
                            }
                        }
                        else
                        {
                            success = true;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"could not apply theme via IThemeManager2");
                }
            })
            {
                Name = "COMThemeManagerThread"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            try
            {
                thread.Join();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "theme handler thread was interrupted");
            }

            return (found, success);

        }

        public static List<Theme2Wrapper> GetThemeList(Interfaces.IThemeManager2 manager)
        {
            List<Theme2Wrapper> list = new();

            int count = 0;
            try
            {
                int res = manager.GetThemeCount(out count);
                if (res != 0)
                {
                    throw new ExternalException($"StatusCode: {res}", res);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"exception in Source GetThemeList->GetCount: ");
                throw;
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    manager.GetTheme(i, out Interfaces.ITheme theme);

                    list.Add(new()
                    {
                        Idx = i,
                        ThemeName = theme.DisplayName
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"exception in Source GetThemeList->GetCount: ");
                    throw;
                }
            }

            return list;
        }
    }

    public class Theme2Wrapper
    {
        public string ThemeName { get; set; }
        public int Idx { get; set; }
    }
}

