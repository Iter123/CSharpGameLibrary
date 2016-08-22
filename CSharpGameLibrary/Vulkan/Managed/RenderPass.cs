﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CSGL.Vulkan.Managed {
    public class RenderPassCreateInfo {
        public VkAttachmentDescription[] Attachments { get; set; }
        public SubpassDescription[] Subpasses { get; set; }
        public VkSubpassDependency[] Dependencies { get; set; }
    }

    public class SubpassDescription {
        public VkPipelineBindPoint PipelineBindPoint { get; set; }
        public VkAttachmentReference[] InputAttachments { get; set; }
        public VkAttachmentReference[] ColorAttachments { get; set; }
        public VkAttachmentReference[] ResolveAttachments { get; set; }
        public VkAttachmentReference[] DepthStencilAttachment { get; set; }
        public uint[] PreserveAttachments { get; set; }

        internal VkSubpassDescription GetNative(List<IDisposable> marshalled) {
            var result = new VkSubpassDescription();

            result.pipelineBindPoint = PipelineBindPoint;

            var inputMarshalled = new MarshalledArray<VkAttachmentReference>(InputAttachments);
            result.inputAttachmentCount = (uint)inputMarshalled.Count;
            result.pInputAttachments = inputMarshalled.Address;

            var colorMarshalled = new MarshalledArray<VkAttachmentReference>(ColorAttachments);
            result.colorAttachmentCount = (uint)colorMarshalled.Count;
            result.pColorAttachments = colorMarshalled.Address;

            var resolveMarshalled = new MarshalledArray<VkAttachmentReference>(ResolveAttachments);
            result.pResolveAttachments = resolveMarshalled.Address;

            var depthMarshalled = new MarshalledArray<VkAttachmentReference>(DepthStencilAttachment);
            result.pDepthStencilAttachment = depthMarshalled.Address;

            if (PreserveAttachments != null) {
                result.preserveAttachmentCount = (uint)PreserveAttachments.Length;
                result.pPreserveAttachments = PreserveAttachments;
            }

            marshalled.Add(inputMarshalled);
            marshalled.Add(colorMarshalled);
            marshalled.Add(resolveMarshalled);
            marshalled.Add(depthMarshalled);

            return result;
        }
    }

    public class RenderPass : IDisposable {
        VkRenderPass renderPass;
        bool disposed = false;

        public Device Device { get; private set; }

        public VkRenderPass Native {
            get {
                return renderPass;
            }
        }

        public RenderPass(Device device, RenderPassCreateInfo info) {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (info == null) throw new ArgumentNullException(nameof(info));

            Device = device;

            CreateRenderPass(info);
        }

        void CreateRenderPass(RenderPassCreateInfo mInfo) {
            var info = new VkRenderPassCreateInfo();
            info.sType = VkStructureType.StructureTypeRenderPassCreateInfo;
            var marshalledArrays = new List<IDisposable>();

            var attachMarshalled = new MarshalledArray<VkAttachmentDescription>(mInfo.Attachments);
            info.attachmentCount = (uint)attachMarshalled.Count;
            info.pAttachments = attachMarshalled.Address;

            var subpasses = new VkSubpassDescription[mInfo.Subpasses.Length];
            for (int i = 0; i < subpasses.Length; i++) {
                subpasses[i] = mInfo.Subpasses[i].GetNative(marshalledArrays);
            }

            var subpassMarshalled = new MarshalledArray<VkSubpassDescription>(subpasses);
            info.subpassCount = (uint)subpassMarshalled.Count;
            info.pSubpasses = subpassMarshalled.Address;

            var dependMarshalled = new MarshalledArray<VkSubpassDependency>(mInfo.Dependencies);
            info.dependencyCount = (uint)dependMarshalled.Count;
            info.pDependencies = dependMarshalled.Address;

            IntPtr infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<VkRenderPassCreateInfo>());
            Marshal.StructureToPtr(info, infoPtr,false);

            IntPtr renderPassPtr = Marshal.AllocHGlobal(Marshal.SizeOf<VkRenderPass>());

            try {
                var result = Device.Commands.createRenderPass(Device.Native, infoPtr, Device.Instance.AllocationCallbacks, renderPassPtr);
                if (result != VkResult.Success) throw new RenderPassException(string.Format("Error creating render pass: {0}"));

                renderPass = Marshal.PtrToStructure<VkRenderPass>(renderPassPtr);
            }
            finally {
                Marshal.DestroyStructure<VkRenderPassCreateInfo>(infoPtr);

                Marshal.FreeHGlobal(infoPtr);
                Marshal.FreeHGlobal(renderPassPtr);

                foreach (var m in marshalledArrays) {
                    m.Dispose();
                }

                attachMarshalled.Dispose();
                subpassMarshalled.Dispose();
                dependMarshalled.Dispose();
            }
        }

        public void Dispose() {
            if (disposed) return;

            Device.Commands.destroyRenderPass(Device.Native, renderPass, Device.Instance.AllocationCallbacks);

            disposed = true;
        }
    }

    public class RenderPassException : Exception {
        public RenderPassException(string message) : base(message) { }
    }
}
