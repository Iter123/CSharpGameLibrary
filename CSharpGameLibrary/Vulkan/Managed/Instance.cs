﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.Vulkan.Unmanaged;

namespace CSGL.Vulkan.Managed {
    public class InstanceCreateInfo {
        public ApplicationInfo ApplicationInfo { get; set; }
        public List<string> Extensions { get; set; }
        public List<string> Layers { get; set; }

        public InstanceCreateInfo(ApplicationInfo appInfo, List<string> extensions, List<string> layers) {
            ApplicationInfo = appInfo;
            Extensions = extensions;
            Layers = layers;
        }
    }

    public class Instance : IDisposable {
        VkInstance instance;
        unsafe VkAllocationCallbacks* alloc = null;
        bool callbacksSet = false;
        bool disposed = false;

        public VkInstance Native {
            get {
                return instance;
            }
        }

        public List<string> Extensions { get; private set; }
        public List<string> Layers { get; private set; }
        public List<PhysicalDevice> PhysicalDevices { get; private set; }

        vkEnumeratePhysicalDevicesDelegate enumeratePhysicalDevicesDel;
        vkGetPhysicalDevicePropertiesDelegate getPhysicalDevicePropertiesDel;

        vkCreateDeviceDelegate createDeviceDel;
        vkDestroyDeviceDelegate destroyDeviceDel;
        vkGetDeviceProcAddrDelegate getDeviceProcAddrDel;

        static vkGetInstanceProcAddrDelegate GetProcAddr;
        static vkCreateInstanceDelegate CreateInstance;
        static vkDestroyInstanceDelegate DestroyInstance;
        static vkEnumerateInstanceExtensionPropertiesDelegate EnumerateExtensionProperties;
        static vkEnumerateInstanceLayerPropertiesDelegate EnumerateLayerProperties;

        public static List<Extension> AvailableExtensions { get; private set; }
        public static List<Layer> AvailableLayers { get; private set; }

        static public IntPtr GetProcAddress(string command) {
            unsafe {
            fixed (byte * ptr = Interop.GetUTF8(command)) {
                return GetProcAddr(VkInstance.Null, ptr);
                }
            }
        }

        public unsafe VkAllocationCallbacks* AllocationCallbacks {
            get {
                return alloc;
            }
        }

        public Instance(InstanceCreateInfo info) {
            CreateInstanceInternal(info);
        }

        public Instance(InstanceCreateInfo info, VkAllocationCallbacks callbacks) {
            unsafe
            {
                alloc = (VkAllocationCallbacks*)Marshal.AllocHGlobal(Marshal.SizeOf<VkAllocationCallbacks>());
                *alloc = callbacks;
            }
            callbacksSet = true;
            CreateInstanceInternal(info);
        }

        internal static void Init() {
            GetProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddrDelegate>(GLFW.GLFW.GetInstanceProcAddress(VkInstance.Null, "vkGetInstanceProcAddr"));
            Vulkan.Load(ref CreateInstance);
            Vulkan.Load(ref DestroyInstance);
            Vulkan.Load(ref EnumerateExtensionProperties);
            Vulkan.Load(ref EnumerateLayerProperties);
            GetLayersAndExtensions();
        }

        static void GetLayersAndExtensions() {
            AvailableExtensions = new List<Extension>();
            AvailableLayers = new List<Layer>();
            unsafe
            {
                uint exCount = 0;
                VkExtensionProperties* exTemp = null;
                EnumerateExtensionProperties(null, ref exCount, ref *exTemp);
                var ex = stackalloc VkExtensionProperties[(int)exCount];
                EnumerateExtensionProperties(null, ref exCount, ref ex[0]);

                for (int i = 0; i < exCount; i++) {
                    var p = ex[i];
                    AvailableExtensions.Add(new Extension(Interop.GetString(p.extensionName), p.specVersion));
                }

                uint lCount = 0;
                VkLayerProperties* lTemp = null;
                EnumerateLayerProperties(ref lCount, ref *lTemp);
                var l = stackalloc VkLayerProperties[(int)lCount];
                EnumerateLayerProperties(ref lCount, ref l[0]);

                for (int i = 0; i < lCount; i++) {
                    var p = l[i];
                    var name = Interop.GetString(p.layerName);
                    var desc = Interop.GetString(p.description);
                    var spec = p.specVersion;
                    var impl = p.implementationVersion;
                    var layer = new Layer(name, desc, spec, impl);
                    AvailableLayers.Add(layer);
                }
            }
        }

        void CreateInstanceInternal(InstanceCreateInfo mInfo) {
            if (mInfo.Extensions == null) {
                Extensions = new List<string>();
            } else {
                Extensions = mInfo.Extensions;
            }
            if (mInfo.Layers == null) {
                Layers = new List<string>();
            } else {
                Layers = mInfo.Layers;
            }

            ValidateExtensions();
            ValidateLayers();

            MakeVulkanInstance(mInfo);

            Vulkan.Load(ref enumeratePhysicalDevicesDel, instance);
            Vulkan.Load(ref getPhysicalDevicePropertiesDel, instance);

            Vulkan.Load(ref createDeviceDel, instance);
            Vulkan.Load(ref destroyDeviceDel, instance);
            Vulkan.Load(ref getDeviceProcAddrDel, instance);

            GetPhysicalDevices();
        }

        void GetPhysicalDevices() {
            PhysicalDevices = new List<PhysicalDevice>();
            unsafe
            {
                uint count = 0;
                VkPhysicalDevice* temp = null;
                enumeratePhysicalDevicesDel(instance, ref count, ref *temp);
                var devices = stackalloc VkPhysicalDevice[(int)count];
                enumeratePhysicalDevicesDel(instance, ref count, ref devices[0]);

                for (int i = 0; i < count; i++) {
                    PhysicalDevices.Add(new PhysicalDevice(this, devices[i]));
                }
            }
        }

        void MakeVulkanInstance(InstanceCreateInfo mInfo) {
            //the managed classes are assembled into Vulkan structs on the stack
            //they can't be members of the class without pinning or fixing them
            //allocating the callback on the unmanaged heap feels messy enough, I didn't want to do that with every struct
            //nor did I want to make InstanceCreateInfo and ApplicationInfo disposable

            unsafe
            {
                VkApplicationInfo appInfo = new VkApplicationInfo();
                VkInstanceCreateInfo info = new VkInstanceCreateInfo();

                info.sType = VkStructureType.StructureTypeInstanceCreateInfo;

                GCHandle appNameHandle = new GCHandle();
                GCHandle engNameHandle = new GCHandle();
                if (mInfo.ApplicationInfo != null) {
                    appInfo.sType = VkStructureType.StructureTypeApplicationInfo;
                    appInfo.apiVersion = mInfo.ApplicationInfo.APIVersion;
                    appInfo.engineVersion = mInfo.ApplicationInfo.EngineVersion;
                    appInfo.applicationVersion = mInfo.ApplicationInfo.ApplicationVersion;
                    info.pApplicationInfo = &appInfo;

                    var appName = Interop.GetUTF8(mInfo.ApplicationInfo.ApplicationName);
                    var engName = Interop.GetUTF8(mInfo.ApplicationInfo.EngineName);
                    appNameHandle = GCHandle.Alloc(appName, GCHandleType.Pinned);
                    engNameHandle = GCHandle.Alloc(engName, GCHandleType.Pinned);
                    appInfo.pApplicationName = (byte*)appNameHandle.AddrOfPinnedObject();
                    appInfo.pEngineName = (byte*)engNameHandle.AddrOfPinnedObject();
                }

                byte** ppExtensionNames = stackalloc byte*[Extensions.Count];
                byte** ppLayerNames = stackalloc byte*[Layers.Count];
                GCHandle* exHandles = stackalloc GCHandle[Extensions.Count];
                GCHandle* lHandles = stackalloc GCHandle[Layers.Count];
                
                for (int i = 0; i < Extensions.Count; i++) {
                    var s = Interop.GetUTF8(Extensions[i]);
                    exHandles[i] = GCHandle.Alloc(s, GCHandleType.Pinned);
                    ppExtensionNames[i] = (byte*)exHandles[i].AddrOfPinnedObject();
                }
                info.enabledExtensionCount = (uint)Extensions.Count;
                if (Extensions.Count > 0) info.ppEnabledExtensionNames = ppExtensionNames;
                
                for (int i = 0; i < Layers.Count; i++) {
                    var s = Interop.GetUTF8(Layers[i]);
                    lHandles[i] = GCHandle.Alloc(s, GCHandleType.Pinned);
                    ppLayerNames[i] = (byte*)lHandles[i].AddrOfPinnedObject();
                }
                info.enabledLayerCount = (uint)Layers.Count;
                if (Layers.Count > 0) info.ppEnabledLayerNames = ppLayerNames;

                var result = CreateInstance(ref info, alloc, ref instance);

                for (int i = 0; i < Extensions.Count; i++) {
                    exHandles[i].Free();
                }
                for (int i = 0; i < Layers.Count; i++) {
                    lHandles[i].Free();
                }

                if (mInfo.ApplicationInfo != null) {
                    appNameHandle.Free();
                    engNameHandle.Free();
                }

                if (result != VkResult.Success) throw new InstanceException(string.Format("Error creating instance: {0}", result));
            }
        }

        void ValidateLayers() {
            foreach (var s in Layers) {
                bool found = false;

                for (int i = 0; i < AvailableLayers.Count; i++) {
                    if (AvailableLayers[i].Name == s) {
                        found = true;
                        break;
                    }
                }

                if (!found) throw new InstanceException(string.Format("Requested layer '{0}' is not available", s));
            }
        }

        void ValidateExtensions() {
            foreach (var s in Extensions) {
                bool found = false;

                for (int i = 0; i < AvailableExtensions.Count; i++) {
                    if (AvailableExtensions[i].Name == s) {
                        found = true;
                        break;
                    }
                }

                if (!found) throw new InstanceException(string.Format("Requested exception '{0}' is not available", s));
            }
        }

        public VkResult CreateDevice(VkPhysicalDevice physicalDevice, ref VkDeviceCreateInfo info, ref VkDevice device) {
            unsafe
            {
                return createDeviceDel(physicalDevice, ref info, alloc, ref device);
            }
        }

        public void DestroyDevice(VkDevice device) {
            unsafe
            {
                destroyDeviceDel(device, alloc);
            }
        }

        public IntPtr GetDeviceProcAddress(VkDevice device, string command) {
            unsafe
            {
                fixed (byte* ptr = Interop.GetUTF8(command)) {
                    return getDeviceProcAddrDel(device, ptr);
                }
            }
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        void Dispose(bool disposing) {
            if (disposed) return;
            unsafe
            {
                DestroyInstance(instance, alloc);
                if (callbacksSet) {
                    Marshal.FreeHGlobal((IntPtr)alloc);
                }
            }

            if (disposing) {
                Extensions = null;
                Layers = null;
                PhysicalDevices = null;

                enumeratePhysicalDevicesDel = null;
                getPhysicalDevicePropertiesDel = null;

                createDeviceDel = null;
                destroyDeviceDel = null;
                getDeviceProcAddrDel = null;
            }

            disposed = true;
        }

        ~Instance() {
            Dispose(false);
        }
    }

    public class InstanceException : Exception {
        public InstanceException(string message) : base(message) { }
    }
}
