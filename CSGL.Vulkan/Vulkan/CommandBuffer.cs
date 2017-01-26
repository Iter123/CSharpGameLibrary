﻿using System;
using System.Collections.Generic;

namespace CSGL.Vulkan {
    public class CommandBufferAllocateInfo {
        public VkCommandBufferLevel level;
        public uint commandBufferCount;
    }

    public class CommandBuffer: INative<VkCommandBuffer> {
        VkCommandBuffer commandBuffer;

        public VkCommandBuffer Native {
            get {
                return commandBuffer;
            }
        }

        public Device Device { get; private set; }
        public CommandPool Pool { get; private set; }

        internal CommandBuffer(Device device, CommandPool pool, VkCommandBuffer commandBuffer) {
            Device = device;
            Pool = pool;
            this.commandBuffer = commandBuffer;
        }

        public void Begin(CommandBufferBeginInfo info) {
            using (var marshalled = new DisposableList<IDisposable>()) {
                var infoNative = info.GetNative(marshalled);
                Device.Commands.beginCommandBuffer(commandBuffer, ref infoNative);
            }
        }

        public void BeginRenderPass(RenderPassBeginInfo info, VkSubpassContents contents) {
            using (var marshalled = new DisposableList<IDisposable>()) {
                var infoNative = info.GetNative(marshalled);
                Device.Commands.cmdBeginRenderPass(commandBuffer, ref infoNative, contents);
            }
        }

        public void BindPipeline(VkPipelineBindPoint bindPoint, Pipeline pipeline) {
            Device.Commands.cmdBindPipeline(commandBuffer, bindPoint, pipeline.Native);
        }

        public void BindVertexBuffers(uint firstBinding, Buffer[] buffers, ulong[] offsets) {
            unsafe
            {
                var buffersNative = stackalloc VkBuffer[buffers.Length];

                Interop.Marshal<VkBuffer>(buffers, buffersNative);

                Device.Commands.cmdBindVertexBuffers(commandBuffer, firstBinding, (uint)buffers.Length, (IntPtr)(buffersNative), ref offsets[0]);
            }
        }

        public void BindVertexBuffers(uint firstBinding, List<Buffer> buffers, ulong[] offsets) {
            unsafe
            {
                var buffersNative = stackalloc VkBuffer[buffers.Count];

                Interop.Marshal(buffers, buffersNative);

                Device.Commands.cmdBindVertexBuffers(commandBuffer, firstBinding, (uint)buffers.Count, (IntPtr)(buffersNative), ref offsets[0]);
            }
        }

        public void BindVertexBuffer(uint firstBinding, Buffer buffer, ulong offset) {
            unsafe
            {
                VkBuffer bufferNative = buffer.Native;
                Device.Commands.cmdBindVertexBuffers(commandBuffer, firstBinding, 1, (IntPtr)(&bufferNative), ref offset);
            }
        }

        public void BindIndexBuffer(Buffer buffer, ulong offset, VkIndexType indexType) {
            Device.Commands.cmdBindIndexBuffer(commandBuffer, buffer.Native, offset, indexType);
        }

        public void BindDescriptorSets(VkPipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint firstSet, DescriptorSet[] descriptorSets, uint[] dynamicOffsets) {
            using (var descriptorSetsMarshalled = new NativeArray<VkDescriptorSet>(descriptorSets))
            using (var offsetsMarshalled = new PinnedArray<uint>(dynamicOffsets)) {
                Device.Commands.cmdBindDescriptorSets(commandBuffer, pipelineBindPoint, layout.Native,
                    firstSet, (uint)descriptorSets.Length, descriptorSetsMarshalled.Address,
                    (uint)offsetsMarshalled.Count, offsetsMarshalled.Address);
            }
        }

        public void BindDescriptorSets(VkPipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint firstSet, DescriptorSet[] descriptorSets) {
            BindDescriptorSets(pipelineBindPoint, layout, firstSet, descriptorSets, null);
        }

        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance) {
            Device.Commands.cmdDraw(commandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance) {
            Device.Commands.cmdDraw(commandBuffer, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
        }

        public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance) {
            Device.Commands.cmdDrawIndexed(commandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
        }

        public void Copy(Buffer srcBuffer, Buffer dstBuffer, VkBufferCopy[] regions) {
            unsafe
            {
                var regionsNative = stackalloc VkBufferCopy[regions.Length];

                Interop.Marshal(regions, regionsNative);

                Device.Commands.cmdCopyBuffer(commandBuffer, srcBuffer.Native, dstBuffer.Native, (uint)regions.Length, (IntPtr)regionsNative);
            }
        }

        public void Copy(Buffer srcBuffer, Buffer dstBuffer) {
            unsafe
            {
                VkBufferCopy region = new VkBufferCopy();
                region.srcOffset = 0;
                region.dstOffset = 0;
                region.size = System.Math.Min(srcBuffer.Size, dstBuffer.Size);
                
                Device.Commands.cmdCopyBuffer(commandBuffer, srcBuffer.Native, dstBuffer.Native, 1, (IntPtr)(&region));
            }
        }

        public void Copy(Buffer srcBuffer, Buffer dstBuffer, VkBufferCopy region) {
            unsafe
            {
                Device.Commands.cmdCopyBuffer(commandBuffer, srcBuffer.Native, dstBuffer.Native, 1, (IntPtr)(&region));
            }
        }

        public void Copy(Image srcImage, VkImageLayout srcImageLayout, Image dstImage, VkImageLayout dstImageLayout, VkImageCopy[] regions) {
            unsafe {
                var regionsNative = stackalloc VkImageCopy[regions.Length];

                Interop.Marshal(regions, regionsNative);

                Device.Commands.cmdCopyImage(commandBuffer,
                    srcImage.Native, srcImageLayout,
                    dstImage.Native, dstImageLayout,
                    (uint)regions.Length, (IntPtr)regionsNative);
            }
        }

        public void PipelineBarrier(VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask, VkDependencyFlags flags,
            MemoryBarrier[] memoryBarriers, BufferMemoryBarrier[] bufferMemoryBarriers, ImageMemoryBarrier[] imageMemoryBarriers) {

            MarshalledArray<VkMemoryBarrier> memoryBarriersMarshalled = null;
            uint mbCount = 0;
            IntPtr mbAddress = IntPtr.Zero;
            MarshalledArray<VkBufferMemoryBarrier> bufferBarriersMarshalled = null;
            uint bbCount = 0;
            IntPtr bbAddress = IntPtr.Zero;
            MarshalledArray<VkImageMemoryBarrier> imageBarriersMarshalled = null;
            uint ibCount = 0;
            IntPtr ibAddress = IntPtr.Zero;

            if (memoryBarriers != null) {
                memoryBarriersMarshalled = new MarshalledArray<VkMemoryBarrier>(memoryBarriers.Length);
                mbCount = (uint)memoryBarriers.Length;
                mbAddress = memoryBarriersMarshalled.Address;

                for (int i = 0; i < memoryBarriers.Length; i++) {
                    var mb = memoryBarriers[i];
                    var barrier = new VkMemoryBarrier();
                    barrier.sType = VkStructureType.MemoryBarrier;
                    barrier.srcAccessMask = mb.srcAccessMask;
                    barrier.dstAccessMask = mb.dstAccessMask;

                    memoryBarriersMarshalled[i] = barrier;
                }
            }

            if (bufferMemoryBarriers != null) {
                bufferBarriersMarshalled = new MarshalledArray<VkBufferMemoryBarrier>(bufferMemoryBarriers.Length);
                bbCount = (uint)bufferMemoryBarriers.Length;
                bbAddress = bufferBarriersMarshalled.Address;

                for (int i = 0; i < bufferMemoryBarriers.Length; i++) {
                    var bb = bufferMemoryBarriers[i];
                    var barrier = new VkBufferMemoryBarrier();
                    barrier.sType = VkStructureType.BufferMemoryBarrier;
                    barrier.srcAccessMask = bb.srcAccessMask;
                    barrier.dstAccessMask = bb.dstAccessMask;
                    barrier.srcQueueFamilyIndex = bb.srcQueueFamilyIndex;
                    barrier.dstQueueFamilyIndex = bb.dstQueueFamilyIndex;
                    barrier.buffer = bb.buffer.Native;
                    barrier.offset = bb.offset;
                    barrier.size = bb.size;

                    bufferBarriersMarshalled[i] = barrier;
                }
            }

            if (imageMemoryBarriers != null) {
                imageBarriersMarshalled = new MarshalledArray<VkImageMemoryBarrier>(imageMemoryBarriers.Length);
                ibCount = (uint)imageMemoryBarriers.Length;
                ibAddress = imageBarriersMarshalled.Address;

                for (int i = 0; i < imageMemoryBarriers.Length; i++) {
                    var ib = imageMemoryBarriers[i];
                    var barrier = new VkImageMemoryBarrier();
                    barrier.sType = VkStructureType.ImageMemoryBarrier;
                    barrier.srcAccessMask = ib.srcAccessMask;
                    barrier.dstAccessMask = ib.dstAccessMask;
                    barrier.oldLayout = ib.oldLayout;
                    barrier.newLayout = ib.newLayout;
                    barrier.srcQueueFamilyIndex = ib.srcQueueFamilyIndex;
                    barrier.dstQueueFamilyIndex = ib.dstQueueFamilyIndex;
                    barrier.image = ib.image.Native;
                    barrier.subresourceRange = ib.subresourceRange;

                    imageBarriersMarshalled[i] = barrier;
                }
            }

            using (memoryBarriersMarshalled)
            using (bufferBarriersMarshalled)
            using (imageBarriersMarshalled) {
                Device.Commands.cmdPipelineBarrier(commandBuffer,
                    srcStageMask, dstStageMask, flags,
                    mbCount, mbAddress,
                    bbCount, bbAddress,
                    ibCount, ibAddress);
            }
        }

        public void ClearColorImage(Image image, VkImageLayout imageLayout, ref VkClearColorValue clearColor, VkImageSubresourceRange[] ranges) {
            unsafe
            {
                var rangesNative = stackalloc VkImageSubresourceRange[ranges.Length];
                Interop.Marshal(ranges, rangesNative);
                Device.Commands.cmdClearColorImage(commandBuffer, image.Native, imageLayout, ref clearColor, (uint)ranges.Length, (IntPtr)rangesNative);
            }
        }

        public void EndRenderPass() {
            Device.Commands.cmdEndRenderPass(commandBuffer);
        }

        public VkResult End() {
            return Device.Commands.endCommandBuffer(commandBuffer);
        }

        public VkResult Reset(VkCommandBufferResetFlags flags) {
            return Device.Commands.resetCommandBuffers(commandBuffer, flags);
        }
    }
}
