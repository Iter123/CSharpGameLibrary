﻿using System;
using System.Collections.Generic;

using CSGL.GLFW.Unmanaged;

namespace CSGL.GLFW {
    public class Monitor : INative<MonitorPtr> {
        static List<Monitor> monitors;
        static Dictionary<MonitorPtr, Monitor> monitorMap;

        public static IList<Monitor> Monitors { get; private set; }
        public static Monitor Primary { get; private set; }

        internal static void Init() {
            GLFW.SetMonitorCallback(MonitorConnection);
            monitors = new List<Monitor>();
            monitorMap = new Dictionary<MonitorPtr, Monitor>();
            Monitors = monitors.AsReadOnly();
        }
        
        static void GetMonitors() {
            monitors.Clear();
            var monitorsNative = GLFW.GetMonitors();

            foreach (var n in monitorsNative) {
                if (monitorMap.ContainsKey(n)) {
                    monitors.Add(monitorMap[n]);
                } else {
                    var m = new Monitor(n);
                    monitors.Add(m);
                    monitorMap.Add(n, m);
                }
            }

            Primary = Monitors[0];
        }

        static void MonitorConnection(MonitorPtr monitor, ConnectionStatus status) {
            if (status == ConnectionStatus.Disconnected) {
                monitorMap[monitor].Status = false;
                monitorMap.Remove(monitor);
            }

            GetMonitors();
        }

        MonitorPtr monitor;
        GammaRamp gammaRamp;

        public MonitorPtr Native {
            get {
                return monitor;
            }
        }

        public string Name { get; private set; }
        public bool Status { get; private set; }
        public IList<VideoMode> VideoModes { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PositionX { get; private set; }
        public int PositionY { get; private set; }

        public GammaRamp GammaRamp {
            get {
                return gammaRamp;
            }
            set {
                GLFW.SetGammaRamp(monitor, value);
                gammaRamp = value;
            }
        }

        Monitor(MonitorPtr monitor) {
            this.monitor = monitor;

            Name = GLFW.GetMonitorName(monitor);

            VideoModes = new List<VideoMode>(GLFW.GetVideoModes(monitor)).AsReadOnly();

            int x, y, w, h;
            GLFW.GetMonitorPhysicalSize(monitor, out w, out h);
            GLFW.GetMonitorPos(monitor, out x, out y);

            Width = w;
            Height = h;
            PositionX = x;
            PositionY = y;

            gammaRamp = GLFW.GetGammaRamp(monitor);
        }

        public void SetGamma(float gamma) {
            GLFW.SetGamma(monitor, gamma);
            gammaRamp = GLFW.GetGammaRamp(monitor);
        }
    }
}
