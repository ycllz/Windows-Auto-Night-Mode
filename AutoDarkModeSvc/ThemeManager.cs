﻿using System;
using AutoDarkModeSvc.Handlers;
using System.Threading.Tasks;
using AutoDarkModeSvc.Config;
using System.Threading;

namespace AutoDarkModeSvc
{
    static class ThemeManager
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static void TimedSwitch(AutoDarkModeConfig config)
        {
            DateTime sunrise = config.Sunrise;
            DateTime sunset = config.Sunset;
            if (config.Location.Enabled)
            {
                LocationHandler.ApplySunDateOffset(config, out sunrise, out sunset);
            }
            //the time bewteen sunrise and sunset, aka "day"
            if (Extensions.NowIsBetweenTimes(sunrise.TimeOfDay, sunset.TimeOfDay))
            {
                SwitchTheme(config, Theme.Light);
            }
            else
            {
                SwitchTheme(config, Theme.Dark);
            }
        }

        public static void SwitchTheme(AutoDarkModeConfig config, Theme newTheme)
        {
            RuntimeConfig rtc = RuntimeConfig.Instance();
            if (config.DarkThemePath == null || config.LightThemePath == null)
            {
                Logger.Error("dark and light theme paths set incorrectly");
                return;
            }
            if (config.LightThemePath.Contains(rtc.CurrentWindowsThemeName) && newTheme == Theme.Dark)
            {
                try
                {
                    ThemeHandler.ChangeTheme(config.DarkThemePath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "couldn't switch to dark theme");
                }
            }
            else if (config.DarkThemePath.Contains(rtc.CurrentWindowsThemeName) && newTheme == Theme.Light)
            {
                try
                {
                    ThemeHandler.ChangeTheme(config.LightThemePath);
                    var updatedTheme = ThemeHandler.GetCurrentThemeName();
                    if (config.LightThemePath.Contains(updatedTheme))
                    {
                        rtc.CurrentWindowsThemeName = updatedTheme;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "couldn't switch to light theme");
                }
            }
        }

        /// <summary>
        /// checks if any theme needs an update because the runtimeconfig differs from the actual configured state
        /// </summary>
        /// <param name="config">AutoDarkModeConfig instance</param>
        /// <param name="newTheme">new theme that is requested</param>
        /// <returns></returns>
        private static bool NeedsUpdate(AutoDarkModeConfig config, Theme newTheme)
        {
            RuntimeConfig rtc = RuntimeConfig.Instance();

            if (config.Wallpaper.Enabled)
            {
                if (rtc.CurrentWallpaperTheme == Theme.Undefined || rtc.CurrentWallpaperTheme != newTheme)
                {
                    return true;
                }
            }

            if ((config.SystemTheme == Mode.DarkOnly && rtc.CurrentSystemTheme != Theme.Dark)
                 || (config.SystemTheme == Mode.LightOnly && rtc.CurrentSystemTheme != Theme.Light)
                 || (config.SystemTheme == Mode.Switch && rtc.CurrentSystemTheme != newTheme)
               )
            {
                return true;
            }

            if ((config.AppsTheme == Mode.DarkOnly && rtc.CurrentAppsTheme != Theme.Dark)
                 || (config.AppsTheme == Mode.LightOnly && rtc.CurrentAppsTheme != Theme.Light)
                 || (config.AppsTheme == Mode.Switch && rtc.CurrentAppsTheme != newTheme)
               )
            {
                return true;
            }

            if ((config.EdgeTheme == Mode.DarkOnly && rtc.CurrentEdgeTheme != Theme.Dark)
                || (config.EdgeTheme == Mode.LightOnly && rtc.CurrentEdgeTheme != Theme.Light)
                || (config.EdgeTheme == Mode.Switch && rtc.CurrentEdgeTheme != newTheme)
              )
            {
                return true;
            }

            return false;
        }

        public static void SwitchThemeClassic(AutoDarkModeConfig config, Theme newTheme)
        {
            RuntimeConfig rtc = RuntimeConfig.Instance();

            if (!NeedsUpdate(config, newTheme))
            {
                return;
            }

            var oldsys = rtc.CurrentSystemTheme;
            var oldapp = rtc.CurrentAppsTheme;
            var oldedg = rtc.CurrentEdgeTheme;
            var oldwal = rtc.CurrentWallpaperTheme;

            if (config.AppsTheme == Mode.DarkOnly)
            {
                RegistryHandler.SetAppsTheme((int)Theme.Dark);
                rtc.CurrentAppsTheme = Theme.Dark;
            }
            else if (config.AppsTheme == Mode.LightOnly)
            {
                RegistryHandler.SetAppsTheme((int)Theme.Light);
                rtc.CurrentAppsTheme = Theme.Light;
            }
            else
            {
                RegistryHandler.SetAppsTheme((int)newTheme);
                rtc.CurrentAppsTheme = newTheme;
            }

            if (config.EdgeTheme == Mode.DarkOnly)
            {
                RegistryHandler.SetEdgeTheme((int)Theme.Dark);
                rtc.CurrentEdgeTheme = Theme.Dark;
            }
            else if (config.EdgeTheme == Mode.LightOnly)
            {
                RegistryHandler.SetEdgeTheme((int)Theme.Light);
                rtc.CurrentEdgeTheme = Theme.Light;
            }
            else
            {
                RegistryHandler.SetEdgeTheme((int)newTheme);
                rtc.CurrentEdgeTheme = newTheme;
            }

            if (config.Wallpaper.Enabled)
            {
                if (newTheme == Theme.Dark)
                {
                    var success = WallpaperHandler.SetBackground(config.Wallpaper.DarkThemeWallpapers);
                    if (success)
                    {
                        rtc.CurrentWallpaperTheme = newTheme;
                    }
                }
                else
                {
                    WallpaperHandler.SetBackground(config.Wallpaper.LightThemeWallpapers);
                    rtc.CurrentWallpaperTheme = newTheme;
                }
            }


            //run async to delay at specific parts due to color prevalence not switching icons correctly
            int taskdelay = config.Tunable.AccentColorSwitchDelay;
            Task.Run(async () =>
            {
                if (config.SystemTheme == Mode.DarkOnly)
                {
                    RegistryHandler.SetSystemTheme((int)Theme.Dark);
                    rtc.CurrentSystemTheme = Theme.Dark;
                    await Task.Delay(taskdelay);
                    if (config.AccentColorTaskbarEnabled)
                    {
                        RegistryHandler.SetColorPrevalence(1);
                    }
                    else
                    {
                        RegistryHandler.SetColorPrevalence(0);
                    }
                }
                else if (config.SystemTheme == Mode.LightOnly)
                {
                    RegistryHandler.SetColorPrevalence(0);
                    await Task.Delay(taskdelay);
                    RegistryHandler.SetSystemTheme((int)Theme.Light);
                    rtc.CurrentSystemTheme = Theme.Light;
                }
                else
                {
                    if (config.AccentColorTaskbarEnabled)
                    {
                        if (newTheme == Theme.Light)
                        {
                            RegistryHandler.SetColorPrevalence(0);
                            await Task.Delay(taskdelay);
                        }
                    }
                    RegistryHandler.SetSystemTheme((int)newTheme);
                    if (config.AccentColorTaskbarEnabled)
                    {
                        if (newTheme == Theme.Dark)
                        {
                            await Task.Delay(taskdelay);
                            RegistryHandler.SetColorPrevalence(1);
                        }
                    }
                    rtc.CurrentSystemTheme = newTheme;
                }

                Logger.Info($"theme switch performed");
                Logger.Info($"theme: {newTheme} with modes (s:{config.SystemTheme}, a:{config.AppsTheme}, e:{config.EdgeTheme}, w:{config.Wallpaper.Enabled})");
                Logger.Info($"was (s:{oldsys}, a:{oldapp}, e:{oldedg}, w:{oldwal}),");
                Logger.Info($"is (s:{rtc.CurrentSystemTheme}, a:{rtc.CurrentAppsTheme}, e:{rtc.CurrentEdgeTheme}, w:{rtc.CurrentWallpaperTheme})");
            });
        }
    }
}