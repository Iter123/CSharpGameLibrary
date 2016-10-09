﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.GLFW;
using CSGL.Vulkan;
using CSGL.Vulkan.Managed;
using CSGL.Vulkan.Unmanaged;

namespace VK_Test2 {
    unsafe class Program : IDisposable {
        static void Main(string[] args) {
            using (var p = new Program()) {
                p.Run();
            }
            Console.ReadLine();
        }

        int width = 800;
        int height = 600;

        string[] extensions = { };
        string[] layers = {
            "VK_LAYER_LUNARG_standard_validation",
            //"VK_LAYER_LUNARG_api_dump"
        };
        string[] deviceExtensions = {
            "VK_KHR_swapchain"
        };

        IntPtr alloc = IntPtr.Zero;

        uint graphicsIndex;
        uint presentIndex;

        WindowPtr window;

        VkSurfaceCapabilitiesKHR capabilities;
        List<VkSurfaceFormatKHR> formats;
        List<VkPresentModeKHR> presentModes;
        VkFormat swapchainFormat;
        VkExtent2D swapchainExtent;

        VkInstance instance;
        VkSurfaceKHR surface;
        VkPhysicalDevice physicalDevice;
        VkDevice device;
        VkQueue graphicsQueue;
        VkQueue presentQueue;
        VkSwapchainKHR swapchain;
        List<VkImage> swapchainImages;
        List<VkImageView> swapchainImageViews;
        VkRenderPass renderPass;
        VkPipelineLayout pipelineLayout;
        VkPipeline graphicsPipeline;
        List<VkFramebuffer> swapchainFramebuffers;
        VkCommandPool commandPool;
        List<VkCommandBuffer> commandBuffers;
        VkSemaphore imageAvailableSemaphore;
        VkSemaphore renderFinishedSemaphore;

        vkCreateInstanceDelegate createInstance;
        vkDestroyInstanceDelegate destroyInstance;

        vkEnumeratePhysicalDevicesDelegate enumeratePhysicalDevices;
        vkGetPhysicalDeviceQueueFamilyPropertiesDelegate getQueueFamilyProperties;

        vkGetPhysicalDeviceSurfaceSupportKHRDelegate getSurfaceSupported;
        vkDestroySurfaceKHRDelegate destroySurface;
        vkGetPhysicalDeviceSurfaceCapabilitiesKHRDelegate getSurfaceCapabilities;
        vkGetPhysicalDeviceSurfaceFormatsKHRDelegate getSurfaceFormats;
        vkGetPhysicalDeviceSurfacePresentModesKHRDelegate getPresentModes;

        vkCreateDeviceDelegate createDevice;
        vkDestroyDeviceDelegate destroyDevice;
        vkGetDeviceQueueDelegate getQueue;

        vkCreateSwapchainKHRDelegate createSwapchain;
        vkDestroySwapchainKHRDelegate destroySwapchain;
        vkGetSwapchainImagesKHRDelegate getSwapchainImages;

        vkCreateImageViewDelegate createImageView;
        vkDestroyImageViewDelegate destroyImageView;

        vkCreateRenderPassDelegate createRenderPass;
        vkDestroyRenderPassDelegate destroyRenderPass;

        vkCreateShaderModuleDelegate createShaderModule;
        vkDestroyShaderModuleDelegate destroyShaderModule;

        vkCreatePipelineLayoutDelegate createPipelineLayout;
        vkDestroyPipelineLayoutDelegate destroyPipelineLayout;
        vkCreateGraphicsPipelinesDelegate createPipelines;
        vkDestroyPipelineDelegate destroyPipeline;

        vkCreateFramebufferDelegate createFramebuffer;
        vkDestroyFramebufferDelegate destroyFramebuffer;

        vkCreateCommandPoolDelegate createCommandPool;
        vkDestroyCommandPoolDelegate destroyCommandPool;
        vkAllocateCommandBuffersDelegate allocateCommandBuffers;

        vkBeginCommandBufferDelegate beginCommandBuffer;
        vkCmdBeginRenderPassDelegate cmdBeginRenderPass;
        vkCmdBindPipelineDelegate cmdBindPipeline;
        vkCmdDrawDelegate cmdDraw;
        vkCmdEndRenderPassDelegate cmdEndRenderPass;
        vkEndCommandBufferDelegate endCommandBuffer;

        vkCreateSemaphoreDelegate createSemaphore;
        vkDestroySemaphoreDelegate destroySemaphore;

        vkAcquireNextImageKHRDelegate acquireNextImage;
        vkQueueSubmitDelegate queueSubmit;
        vkQueuePresentKHRDelegate queuePresent;

        vkDeviceWaitIdleDelegate deviceWaitIdle;

        public Program() {
            GLFW.Init();
        }

        public void Dispose() {
            destroySemaphore(device, imageAvailableSemaphore, alloc);
            destroySemaphore(device, renderFinishedSemaphore, alloc);
            destroyCommandPool(device, commandPool, alloc);
            foreach (var fb in swapchainFramebuffers) destroyFramebuffer(device, fb, alloc);
            destroyPipeline(device, graphicsPipeline, alloc);
            destroyPipelineLayout(device, pipelineLayout, alloc);
            destroyRenderPass(device, renderPass, alloc);
            foreach (var iv in swapchainImageViews) destroyImageView(device, iv, alloc);
            destroySwapchain(device, swapchain, alloc);
            destroyDevice(device, alloc);
            destroySurface(instance, surface, alloc);
            destroyInstance(instance, alloc);
            GLFW.Terminate();
        }

        public void Run() {
            GLFW.WindowHint(WindowHint.ClientAPI, (int)ContextAPI.NoAPI);
            window = GLFW.CreateWindow(width, height, "Test2", MonitorPtr.Null, WindowPtr.Null);

            LoadVulkan();
            CreateInstance();
            PickPhysicalDevice();
            CreateSurface();
            GetIndices();
            CreateDevice();
            CreateSwapchain();
            CreateImageViews();
            CreateRenderPass();
            CreatePipeline();
            CreateFramebuffers();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSemaphores();

            MainLoop();

            GLFW.DestroyWindow(window);
        }

        void MainLoop() {
            var submitInfo = new VkSubmitInfo();
            submitInfo.sType = VkStructureType.StructureTypeSubmitInfo;
            VkSemaphore waitSemaphore = imageAvailableSemaphore;
            VkSemaphore signalSemaphore = renderFinishedSemaphore;
            VkPipelineStageFlags waitStage = VkPipelineStageFlags.PipelineStageColorAttachmentOutputBit;
            submitInfo.waitSemaphoreCount = 1;
            submitInfo.pWaitSemaphores = (IntPtr)(&waitSemaphore);
            submitInfo.pWaitDstStageMask = (IntPtr)(&waitStage);
            submitInfo.signalSemaphoreCount = 1;
            submitInfo.pSignalSemaphores = (IntPtr)(&signalSemaphore);
            submitInfo.commandBufferCount = 1;
            //submitInfo.pCommandBuffers changed every frame, see below

            var presentInfo = new VkPresentInfoKHR();
            presentInfo.sType = VkStructureType.StructureTypePresentInfoKhr;
            presentInfo.waitSemaphoreCount = 1;
            presentInfo.pWaitSemaphores = (IntPtr)(&signalSemaphore);
            VkSwapchainKHR swapchains = swapchain;
            presentInfo.swapchainCount = 1;
            presentInfo.pSwapchains = (IntPtr)(&swapchains);
            //presentInfo.pImageIndices changed every frame, see below

            var sMarshalled = new Marshalled<VkSubmitInfo>(submitInfo);
            var pMarshalled = new Marshalled<VkPresentInfoKHR>(presentInfo);

            while (!GLFW.WindowShouldClose(window)) {
                GLFW.PollEvents();

                uint index;
                acquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphore, VkFence.Null, out index);

                VkCommandBuffer buffer = commandBuffers[(int)index];
                submitInfo.pCommandBuffers = (IntPtr)(&buffer);
                sMarshalled.Value = submitInfo;

                presentInfo.pImageIndices = (IntPtr)(&index);
                pMarshalled.Value = presentInfo;

                queueSubmit(graphicsQueue, 1, sMarshalled.Address, VkFence.Null);
                queuePresent(presentQueue, pMarshalled.Address);
            }

            sMarshalled.Dispose();
            pMarshalled.Dispose();
            deviceWaitIdle(device);
        }

        void CreateInstance() {
            VkInstanceCreateInfo createInfo = new VkInstanceCreateInfo();
            createInfo.sType = VkStructureType.StructureTypeInstanceCreateInfo;

            string[] requiredExtensions = GLFW.GetRequiredInstanceExceptions();

            var extensionsMarshalled = new MarshalledStringArray(requiredExtensions);
            var layersMarshalled = new MarshalledStringArray(layers);

            createInfo.enabledExtensionCount = (uint)requiredExtensions.Length;
            createInfo.ppEnabledExtensionNames = extensionsMarshalled.Address;
            createInfo.enabledLayerCount = (uint)layers.Length;
            createInfo.ppEnabledLayerNames = layersMarshalled.Address;

            var result = VK.CreateInstance(ref createInfo, alloc, out instance);

            extensionsMarshalled.Dispose();
            layersMarshalled.Dispose();
        }

        void PickPhysicalDevice() {
            uint count = 0;
            enumeratePhysicalDevices(instance, ref count, IntPtr.Zero);
            byte* marshalled = stackalloc byte[(int)count * Interop.SizeOf<VkPhysicalDevice>()];
            enumeratePhysicalDevices(instance, ref count, (IntPtr)marshalled);

            physicalDevice = Interop.Unmarshal<VkPhysicalDevice>(marshalled);
        }

        void CreateSurface() {
            VkSurfaceKHR temp;
            GLFW.CreateWindowSurface(instance, window, alloc, (IntPtr)(&temp));
            surface = temp;
        }

        void GetIndices() {
            int gIndex = -1;
            int pIndex = -1;

            uint count = 0;
            getQueueFamilyProperties(physicalDevice, ref count, IntPtr.Zero);
            byte* marshalled = stackalloc byte[(int)count * Interop.SizeOf<VkQueueFamilyProperties>()];
            getQueueFamilyProperties(physicalDevice, ref count, (IntPtr)marshalled);

            for (int i = 0; i < count; i++) {
                var queue = Interop.Unmarshal<VkQueueFamilyProperties>(marshalled, i);
                if (gIndex == -1 && (queue.queueFlags & VkQueueFlags.QueueGraphicsBit) != 0) {
                    gIndex = i;
                }
                if (pIndex == -1) {
                    uint supported = 0;
                    getSurfaceSupported(physicalDevice, (uint)i, surface, ref supported);
                    if (supported != 0) {
                        pIndex = i;
                    }
                }
            }

            graphicsIndex = (uint)gIndex;
            presentIndex = (uint)pIndex;
        }

        void CreateDevice() {
            float priorities = 1;
            HashSet<uint> uniqueQueues = new HashSet<uint> { graphicsIndex, presentIndex };
            VkDeviceQueueCreateInfo[] infos = new VkDeviceQueueCreateInfo[uniqueQueues.Count];
            int count = 0;

            foreach (var index in uniqueQueues) {
                VkDeviceQueueCreateInfo qInfo = new VkDeviceQueueCreateInfo();
                qInfo.sType = VkStructureType.StructureTypeDeviceQueueCreateInfo;
                qInfo.pQueuePriorities = (IntPtr)(&priorities);
                qInfo.queueCount = 1;
                qInfo.queueFamilyIndex = index;
                infos[count] = qInfo;
                count++;
            }

            byte* queueInfosMarshalled = stackalloc byte[Interop.SizeOf<VkDeviceQueueCreateInfo>() * count];
            Interop.Marshal(queueInfosMarshalled, infos);

            VkDeviceCreateInfo info = new VkDeviceCreateInfo();
            info.sType = VkStructureType.StructureTypeDeviceCreateInfo;
            info.pQueueCreateInfos = (IntPtr)queueInfosMarshalled;
            info.queueCreateInfoCount = (uint)count;

            MarshalledStringArray extensions = new MarshalledStringArray(deviceExtensions);
            info.ppEnabledExtensionNames = extensions.Address;
            info.enabledExtensionCount = (uint)deviceExtensions.Length;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkDeviceCreateInfo>()];
            Interop.Marshal(marshalled, info);

            VkDevice temp;

            createDevice(physicalDevice, (IntPtr)marshalled, alloc, (IntPtr)(&temp));
            device = temp;

            extensions.Dispose();

            VkQueue gTemp;
            VkQueue pTemp;
            getQueue(device, graphicsIndex, 0, (IntPtr)(&gTemp));
            getQueue(device, presentIndex, 0, (IntPtr)(&pTemp));

            graphicsQueue = gTemp;
            presentQueue = pTemp;
        }

        void CreateSwapchain() {
            GetSwapchainSupport();

            var format = ChooseSurfaceFormat(formats);
            var mode = ChoosePresentMode(presentModes);
            var extent = ChooseExtent(ref capabilities);

            uint imageCount = capabilities.minImageCount + 1;
            if (capabilities.maxImageCount > 0 && imageCount > capabilities.maxImageCount) {
                imageCount = capabilities.maxImageCount;
            }

            var info = new VkSwapchainCreateInfoKHR();
            info.sType = VkStructureType.StructureTypeSwapchainCreateInfoKhr;
            info.surface = surface;
            info.minImageCount = imageCount;
            info.imageFormat = format.format;
            info.imageColorSpace = format.colorSpace;
            info.imageExtent = extent;
            info.imageArrayLayers = 1;
            info.imageUsage = VkImageUsageFlags.ImageUsageColorAttachmentBit;

            MarshalledArray<uint> indices = new MarshalledArray<uint>(2);
            indices[0] = graphicsIndex;
            indices[1] = presentIndex;

            if (graphicsIndex != presentIndex) {
                info.imageSharingMode = VkSharingMode.SharingModeConcurrent;
                info.queueFamilyIndexCount = 2;
                info.pQueueFamilyIndices = indices.Address;
            } else {
                info.imageSharingMode = VkSharingMode.SharingModeExclusive;
            }

            info.preTransform = capabilities.currentTransform;
            info.compositeAlpha = VkCompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
            info.presentMode = mode;
            info.clipped = 1;
            info.oldSwapchain = VkSwapchainKHR.Null;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkSwapchainCreateInfoKHR>()];
            Interop.Marshal(marshalled, info);
            VkSwapchainKHR temp;

            createSwapchain(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));

            swapchain = temp;

            getSwapchainImages(device, swapchain, ref imageCount, IntPtr.Zero);
            byte* imagesMarshalled = stackalloc byte[Interop.SizeOf<VkImage>() * (int)imageCount];
            getSwapchainImages(device, swapchain, ref imageCount, (IntPtr)imagesMarshalled);

            swapchainImages = new List<VkImage>();
            for (int i = 0; i < imageCount; i++) {
                swapchainImages.Add(Interop.Unmarshal<VkImage>(imagesMarshalled, i));
            }

            swapchainFormat = format.format;
            swapchainExtent = extent;
        }

        void CreateImageViews() {
            swapchainImageViews = new List<VkImageView>(swapchainImages.Count);
            for (int i = 0; i < swapchainImages.Count; i++) {
                var info = new VkImageViewCreateInfo();
                info.sType = VkStructureType.StructureTypeImageViewCreateInfo;
                info.image = swapchainImages[i];
                info.viewType = VkImageViewType.ImageViewType2d;
                info.format = swapchainFormat;
                info.components.r = VkComponentSwizzle.ComponentSwizzleIdentity;
                info.components.g = VkComponentSwizzle.ComponentSwizzleIdentity;
                info.components.b = VkComponentSwizzle.ComponentSwizzleIdentity;
                info.components.a = VkComponentSwizzle.ComponentSwizzleIdentity;
                info.subresourceRange.aspectMask = VkImageAspectFlags.ImageAspectColorBit;
                info.subresourceRange.baseMipLevel = 0;
                info.subresourceRange.levelCount = 1;
                info.subresourceRange.baseArrayLayer = 0;
                info.subresourceRange.layerCount = 1;

                byte* marshalled = stackalloc byte[Interop.SizeOf<VkImageViewCreateInfo>()];
                Interop.Marshal<VkImageViewCreateInfo>(marshalled, info);

                VkImageView temp;

                createImageView(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));

                swapchainImageViews.Add(temp);
            }
        }

        void CreateRenderPass() {
            var colorAttachment = new VkAttachmentDescription();
            colorAttachment.format = swapchainFormat;
            colorAttachment.samples = VkSampleCountFlags.SampleCount1Bit;
            colorAttachment.loadOp = VkAttachmentLoadOp.AttachmentLoadOpClear;
            colorAttachment.storeOp = VkAttachmentStoreOp.AttachmentStoreOpStore;
            colorAttachment.stencilLoadOp = VkAttachmentLoadOp.AttachmentLoadOpClear;
            colorAttachment.stencilStoreOp = VkAttachmentStoreOp.AttachmentStoreOpDontCare;
            colorAttachment.initialLayout = VkImageLayout.ImageLayoutUndefined;
            colorAttachment.finalLayout = VkImageLayout.ImageLayoutPresentSrcKhr;
            byte* caMarshalled = stackalloc byte[Interop.SizeOf<VkAttachmentDescription>()];
            Interop.Marshal(caMarshalled, colorAttachment);

            var colorAttachmentRef = new VkAttachmentReference();
            colorAttachmentRef.attachment = 0;
            colorAttachmentRef.layout = VkImageLayout.ImageLayoutColorAttachmentOptimal;
            byte* carMarshalled = stackalloc byte[Interop.SizeOf<VkAttachmentReference>()];
            Interop.Marshal(carMarshalled, colorAttachmentRef);

            var subpass = new VkSubpassDescription();
            subpass.pipelineBindPoint = VkPipelineBindPoint.PipelineBindPointGraphics;
            subpass.colorAttachmentCount = 1;
            subpass.pColorAttachments = (IntPtr)carMarshalled;
            byte* sMarshalled = stackalloc byte[Interop.SizeOf<VkSubpassDescription>()];
            Interop.Marshal(sMarshalled, subpass);

            var dependency = new VkSubpassDependency();
            dependency.srcSubpass = ~(uint)0;
            dependency.dstSubpass = 0;
            dependency.srcStageMask = VkPipelineStageFlags.PipelineStageBottomOfPipeBit;
            dependency.srcAccessMask = VkAccessFlags.AccessMemoryReadBit;
            dependency.dstStageMask = VkPipelineStageFlags.PipelineStageColorAttachmentOutputBit;
            dependency.dstAccessMask = VkAccessFlags.AccessColorAttachmentReadBit |
                                        VkAccessFlags.AccessColorAttachmentWriteBit;

            var info = new VkRenderPassCreateInfo();
            info.sType = VkStructureType.StructureTypeRenderPassCreateInfo;
            info.attachmentCount = 1;
            info.pAttachments = (IntPtr)caMarshalled;
            info.subpassCount = 1;
            info.pSubpasses = (IntPtr)sMarshalled;
            info.dependencyCount = 1;
            info.pDependencies = (IntPtr)(&dependency);
            byte* infoMarshalled = stackalloc byte[Interop.SizeOf<VkRenderPassCreateInfo>()];
            Interop.Marshal(infoMarshalled, info);

            VkRenderPass temp;

            createRenderPass(device, (IntPtr)infoMarshalled, alloc, (IntPtr)(&temp));
            renderPass = temp;
        }

        void CreatePipeline() {
            var vertCode = File.ReadAllBytes("vert.spv");
            var fragCode = File.ReadAllBytes("frag.spv");

            var vertModule = CreateShaderModule(vertCode);
            var fragModule = CreateShaderModule(fragCode);

            var entry = new InteropString("main");

            var vertInfo = new VkPipelineShaderStageCreateInfo();
            vertInfo.sType = VkStructureType.StructureTypePipelineShaderStageCreateInfo;
            vertInfo.stage = VkShaderStageFlags.ShaderStageVertexBit;
            vertInfo.module = vertModule;
            vertInfo.pName = entry.Address;

            var fragInfo = new VkPipelineShaderStageCreateInfo();
            fragInfo.sType = VkStructureType.StructureTypePipelineShaderStageCreateInfo;
            fragInfo.stage = VkShaderStageFlags.ShaderStageFragmentBit;
            fragInfo.module = fragModule;
            fragInfo.pName = entry.Address;

            var shaderStages = new MarshalledArray<VkPipelineShaderStageCreateInfo>(2);
            shaderStages[0] = vertInfo;
            shaderStages[1] = fragInfo;

            var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo();
            vertexInputInfo.sType = VkStructureType.StructureTypePipelineVertexInputStateCreateInfo;
            byte* viiMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineVertexInputStateCreateInfo>()];
            Interop.Marshal(viiMarshalled, vertexInputInfo);

            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo();
            inputAssembly.sType = VkStructureType.StructureTypePipelineInputAssemblyStateCreateInfo;
            inputAssembly.topology = VkPrimitiveTopology.PrimitiveTopologyTriangleList;
            byte* iaMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineInputAssemblyStateCreateInfo>()];
            Interop.Marshal(iaMarshalled, inputAssembly);

            var viewport = new VkViewport();
            viewport.width = swapchainExtent.width;
            viewport.height = swapchainExtent.height;
            viewport.minDepth = 0;
            viewport.maxDepth = 1;
            byte* vMarshalled = stackalloc byte[Interop.SizeOf<VkViewport>()];
            Interop.Marshal(vMarshalled, viewport);

            var scissor = new VkRect2D();
            scissor.extent = swapchainExtent;
            byte* sMarshalled = stackalloc byte[Interop.SizeOf<VkRect2D>()];
            Interop.Marshal(sMarshalled, scissor);

            var viewportState = new VkPipelineViewportStateCreateInfo();
            viewportState.sType = VkStructureType.StructureTypePipelineViewportStateCreateInfo;
            viewportState.viewportCount = 1;
            viewportState.pViewports = (IntPtr)vMarshalled;
            viewportState.scissorCount = 1;
            viewportState.pScissors = (IntPtr)sMarshalled;
            byte* vsMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineViewportStateCreateInfo>()];
            Interop.Marshal(vsMarshalled, viewportState);

            var rasterizer = new VkPipelineRasterizationStateCreateInfo();
            rasterizer.sType = VkStructureType.StructureTypePipelineRasterizationStateCreateInfo;
            rasterizer.polygonMode = VkPolygonMode.PolygonModeFill;
            rasterizer.lineWidth = 1;
            rasterizer.cullMode = VkCullModeFlags.CullModeBackBit;
            rasterizer.frontFace = VkFrontFace.FrontFaceClockwise;
            byte* rMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineRasterizationStateCreateInfo>()];
            Interop.Marshal(rMarshalled, rasterizer);

            var multisampling = new VkPipelineMultisampleStateCreateInfo();
            multisampling.sType = VkStructureType.StructureTypePipelineMultisampleStateCreateInfo;
            multisampling.rasterizationSamples = VkSampleCountFlags.SampleCount1Bit;
            byte* mMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineMultisampleStateCreateInfo>()];
            Interop.Marshal(mMarshalled, multisampling);

            var colorBlendAttachment = new VkPipelineColorBlendAttachmentState();
            colorBlendAttachment.colorWriteMask = VkColorComponentFlags.ColorComponentRBit
                                                | VkColorComponentFlags.ColorComponentGBit
                                                | VkColorComponentFlags.ColorComponentGBit
                                                | VkColorComponentFlags.ColorComponentABit;
            colorBlendAttachment.srcColorBlendFactor = VkBlendFactor.BlendFactorOne;
            colorBlendAttachment.dstColorBlendFactor = VkBlendFactor.BlendFactorZero;
            colorBlendAttachment.colorBlendOp = VkBlendOp.BlendOpAdd;
            colorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.BlendFactorOne;
            colorBlendAttachment.dstColorBlendFactor = VkBlendFactor.BlendFactorZero;
            colorBlendAttachment.alphaBlendOp = VkBlendOp.BlendOpAdd;
            byte* cbaMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineColorBlendAttachmentState>()];
            Interop.Marshal(cbaMarshalled, colorBlendAttachment);

            var colorBlending = new VkPipelineColorBlendStateCreateInfo();
            colorBlending.sType = VkStructureType.StructureTypePipelineColorBlendStateCreateInfo;
            colorBlending.attachmentCount = 1;
            colorBlending.pAttachments = (IntPtr)cbaMarshalled;
            var cbMarshalled = new Marshalled<VkPipelineColorBlendStateCreateInfo>(colorBlending);  //for some reason, doesn't marshal using stackalloc'd memory

            var pipelineLayoutInfo = new VkPipelineLayoutCreateInfo();
            pipelineLayoutInfo.sType = VkStructureType.StructureTypePipelineLayoutCreateInfo;
            byte* pliMarshalled = stackalloc byte[Interop.SizeOf<VkPipelineLayout>()];
            Interop.Marshal(pliMarshalled, pipelineLayoutInfo);

            {
                VkPipelineLayout temp;

                createPipelineLayout(device, (IntPtr)pliMarshalled, alloc, (IntPtr)(&temp));
                pipelineLayout = temp;
            }

            {
                var info = new VkGraphicsPipelineCreateInfo();
                info.sType = VkStructureType.StructureTypeGraphicsPipelineCreateInfo;
                info.stageCount = 2;
                info.pStages = shaderStages.Address;
                info.pVertexInputState = (IntPtr)viiMarshalled;
                info.pInputAssemblyState = (IntPtr)iaMarshalled;
                info.pViewportState = (IntPtr)vsMarshalled;
                info.pRasterizationState = (IntPtr)rMarshalled;
                info.pMultisampleState = (IntPtr)mMarshalled;
                info.pColorBlendState = (IntPtr)cbMarshalled.Address;
                info.layout = pipelineLayout;
                info.renderPass = renderPass;
                byte* infoMarshalled = stackalloc byte[Interop.SizeOf<VkGraphicsPipelineCreateInfo>()];
                Interop.Marshal(infoMarshalled, info);

                VkPipeline temp;

                createPipelines(device, VkPipelineCache.Null, 1, (IntPtr)infoMarshalled, alloc, (IntPtr)(&temp));
                graphicsPipeline = temp;
            }

            destroyShaderModule(device, vertModule, alloc);
            destroyShaderModule(device, fragModule, alloc);
            shaderStages.Dispose();
            cbMarshalled.Dispose();
        }

        void CreateFramebuffers() {
            swapchainFramebuffers = new List<VkFramebuffer>();

            for (int i = 0; i < swapchainImageViews.Count; i++) {
                VkImageView* attachments = stackalloc VkImageView[1];
                attachments[0] = swapchainImageViews[i];

                var info = new VkFramebufferCreateInfo();
                info.sType = VkStructureType.StructureTypeFramebufferCreateInfo;
                info.renderPass = renderPass;
                info.attachmentCount = 1;
                info.pAttachments = (IntPtr)attachments;
                info.width = swapchainExtent.width;
                info.height = swapchainExtent.height;
                info.layers = 1;

                byte* marshalled = stackalloc byte[Interop.SizeOf<VkFramebufferCreateInfo>()];
                Interop.Marshal(marshalled, info);

                VkFramebuffer temp;

                createFramebuffer(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));

                swapchainFramebuffers.Add(temp);
            }
        }

        void CreateCommandPool() {
            var info = new VkCommandPoolCreateInfo();
            info.sType = VkStructureType.StructureTypeCommandPoolCreateInfo;
            info.queueFamilyIndex = graphicsIndex;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkCommandPoolCreateInfo>()];
            Interop.Marshal(marshalled, info);

            VkCommandPool temp;

            createCommandPool(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));

            commandPool = temp;
        }

        void CreateCommandBuffers() {
            commandBuffers = new List<VkCommandBuffer>(swapchainFramebuffers.Count);

            var info = new VkCommandBufferAllocateInfo();
            info.sType = VkStructureType.StructureTypeCommandBufferAllocateInfo;
            info.commandPool = commandPool;
            info.level = VkCommandBufferLevel.CommandBufferLevelPrimary;
            info.commandBufferCount = (uint)commandBuffers.Capacity;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkCommandBufferAllocateInfo>()];
            Interop.Marshal(marshalled, info);

            VkCommandBuffer* temp = stackalloc VkCommandBuffer[commandBuffers.Capacity];

            allocateCommandBuffers(device, (IntPtr)marshalled, (IntPtr)temp);

            for (int i = 0; i < commandBuffers.Capacity; i++) {
                commandBuffers.Add(temp[i]);

                var beginInfo = new VkCommandBufferBeginInfo();
                beginInfo.sType = VkStructureType.StructureTypeCommandBufferBeginInfo;
                beginInfo.flags = VkCommandBufferUsageFlags.CommandBufferUsageSimultaneousUseBit;

                byte* beginMarshalled = stackalloc byte[Interop.SizeOf<VkCommandBufferBeginInfo>()];
                Interop.Marshal(beginMarshalled, beginInfo);

                beginCommandBuffer(commandBuffers[i], (IntPtr)beginMarshalled);

                var renderPassInfo = new VkRenderPassBeginInfo();
                renderPassInfo.sType = VkStructureType.StructureTypeRenderPassBeginInfo;
                renderPassInfo.renderPass = renderPass;
                renderPassInfo.framebuffer = swapchainFramebuffers[i];
                renderPassInfo.renderArea.extent = swapchainExtent;

                VkClearValue clearColor = new VkClearValue();
                clearColor.color.uint32 = new CSGL.Graphics.Color32(0, 0, 0, 1).ToUint();
                renderPassInfo.clearValueCount = 1;
                renderPassInfo.pClearValues = (IntPtr)(&clearColor);

                var rpMarshalled = new Marshalled<VkRenderPassBeginInfo>(renderPassInfo);   //for some reason doesn't work with stackalloc

                cmdBeginRenderPass(commandBuffers[i], rpMarshalled.Address, VkSubpassContents.SubpassContentsInline);

                cmdBindPipeline(commandBuffers[i], VkPipelineBindPoint.PipelineBindPointGraphics, graphicsPipeline);
                cmdDraw(commandBuffers[i], 3, 1, 0, 0);
                cmdEndRenderPass(commandBuffers[i]);

                endCommandBuffer(commandBuffers[i]);

                rpMarshalled.Dispose();
            }
        }

        void CreateSemaphores() {
            var info = new VkSemaphoreCreateInfo();
            info.sType = VkStructureType.StructureTypeSemaphoreCreateInfo;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkSemaphoreCreateInfo>()];
            Interop.Marshal(marshalled, info);

            VkSemaphore temp;

            createSemaphore(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));
            imageAvailableSemaphore = temp;
            createSemaphore(device, (IntPtr)marshalled, alloc, (IntPtr)(&temp));
            renderFinishedSemaphore = temp;
        }

        VkShaderModule CreateShaderModule(byte[] code) {
            var info = new VkShaderModuleCreateInfo();
            info.sType = VkStructureType.StructureTypeShaderModuleCreateInfo;
            info.codeSize = (ulong)code.LongLength;
            GCHandle handle = GCHandle.Alloc(code, GCHandleType.Pinned);
            info.pCode = handle.AddrOfPinnedObject();

            VkShaderModule result;

            byte* marshalled = stackalloc byte[Interop.SizeOf<VkShaderModuleCreateInfo>()];
            Interop.Marshal(marshalled, info);

            createShaderModule(device, (IntPtr)marshalled, alloc, (IntPtr)(&result));

            handle.Free();
            return result;
        }

        void GetSwapchainSupport() {
            using (Marshalled<VkSurfaceCapabilitiesKHR> capMarshalled = new Marshalled<VkSurfaceCapabilitiesKHR>()) {
                getSurfaceCapabilities(physicalDevice, surface, capMarshalled.Address);
                capabilities = capMarshalled.Value;
            }

            uint count = 0;
            getSurfaceFormats(physicalDevice, surface, ref count, IntPtr.Zero);
            VkSurfaceFormatKHR* formatsMarshalled = stackalloc VkSurfaceFormatKHR[(int)count];
            getSurfaceFormats(physicalDevice, surface, ref count, (IntPtr)formatsMarshalled);

            formats = new List<VkSurfaceFormatKHR>();
            for (int i = 0; i < count; i++) {
                formats.Add(formatsMarshalled[i]);
            }

            getPresentModes(physicalDevice, surface, ref count, IntPtr.Zero);
            byte* modesMarshalled = stackalloc byte[Interop.SizeOf<int>() * (int)count];
            getPresentModes(physicalDevice, surface, ref count, (IntPtr)modesMarshalled);

            presentModes = new List<VkPresentModeKHR>();
            for (int i = 0; i < count; i++) {
                presentModes.Add((VkPresentModeKHR)Interop.Unmarshal<int>(modesMarshalled, i));
            }
        }

        VkSurfaceFormatKHR ChooseSurfaceFormat(List<VkSurfaceFormatKHR> available) {
            if (available.Count == 1 && available[0].format == VkFormat.FormatUndefined) {
                var result = new VkSurfaceFormatKHR();
                result.format = VkFormat.FormatB8g8r8a8Unorm;
                result.colorSpace = VkColorSpaceKHR.ColorSpaceSrgbNonlinearKhr;
                return result;
            }

            foreach (var f in available) {
                if (f.format == VkFormat.FormatB8g8r8a8Unorm &&
                    f.colorSpace == VkColorSpaceKHR.ColorSpaceSrgbNonlinearKhr) {
                    return f;
                }
            }

            return available[0];
        }

        VkPresentModeKHR ChoosePresentMode(List<VkPresentModeKHR> available) {
            foreach (var m in available) {
                if (m == VkPresentModeKHR.PresentModeMailboxKhr) {
                    return m;
                }
            }
            return VkPresentModeKHR.PresentModeFifoKhr;
        }

        VkExtent2D ChooseExtent(ref VkSurfaceCapabilitiesKHR cap) {
            if (cap.currentExtent.width != uint.MaxValue) {
                return cap.currentExtent;
            } else {
                var actual = new VkExtent2D();

                actual.width = Math.Max(cap.minImageExtent.width,
                    Math.Min(cap.maxImageExtent.width, (uint)width));
                actual.height = Math.Max(cap.minImageExtent.height,
                    Math.Min(cap.maxImageExtent.height, (uint)height));

                return actual;
            }
        }

        void LoadVulkan() {
            Vulkan.Load(ref createInstance);
            Vulkan.Load(ref destroyInstance);
            Vulkan.Load(ref enumeratePhysicalDevices);
            Vulkan.Load(ref getQueueFamilyProperties);
            Vulkan.Load(ref getSurfaceSupported);
            Vulkan.Load(ref destroySurface);
            Vulkan.Load(ref createDevice);
            Vulkan.Load(ref destroyDevice);
            Vulkan.Load(ref getQueue);
            Vulkan.Load(ref getSurfaceCapabilities);
            Vulkan.Load(ref getSurfaceFormats);
            Vulkan.Load(ref getPresentModes);
            Vulkan.Load(ref createSwapchain);
            Vulkan.Load(ref destroySwapchain);
            Vulkan.Load(ref getSwapchainImages);
            Vulkan.Load(ref createImageView);
            Vulkan.Load(ref destroyImageView);
            Vulkan.Load(ref createRenderPass);
            Vulkan.Load(ref destroyRenderPass);
            Vulkan.Load(ref createShaderModule);
            Vulkan.Load(ref destroyShaderModule);
            Vulkan.Load(ref createPipelineLayout);
            Vulkan.Load(ref destroyPipelineLayout);
            Vulkan.Load(ref createPipelines);
            Vulkan.Load(ref destroyPipeline);
            Vulkan.Load(ref createFramebuffer);
            Vulkan.Load(ref destroyFramebuffer);
            Vulkan.Load(ref createCommandPool);
            Vulkan.Load(ref destroyCommandPool);
            Vulkan.Load(ref allocateCommandBuffers);
            Vulkan.Load(ref beginCommandBuffer);
            Vulkan.Load(ref cmdBeginRenderPass);
            Vulkan.Load(ref cmdBindPipeline);
            Vulkan.Load(ref cmdDraw);
            Vulkan.Load(ref cmdEndRenderPass);
            Vulkan.Load(ref endCommandBuffer);
            Vulkan.Load(ref createSemaphore);
            Vulkan.Load(ref destroySemaphore);
            Vulkan.Load(ref acquireNextImage);
            Vulkan.Load(ref queueSubmit);
            Vulkan.Load(ref queuePresent);
            Vulkan.Load(ref deviceWaitIdle);
        }
    }
}