﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CSGL {
    public class NativeArray<T> : IDisposable where T : struct {
        int count;
        unsafe void* ptr;
        bool disposed = false;
        bool allocated = false;
        static int elementSize;

        static NativeArray() {
            elementSize = Unsafe.SizeOf<T>();
        }

        public NativeArray(int count) {
            this.count = count;
            Allocate(count);
        }

        public NativeArray(uint count) : this((int)count) { }

        public NativeArray(T[] array) {
            if (array != null) {
                count = array.Length;
                Allocate(array.Length);
                unsafe
                {
                    for (int i = 0; i < count; i++) {
                        Interop.Copy(array, (IntPtr)ptr);
                    }
                }
            }
        }

        public NativeArray(List<T> list) {
            if (list != null) {
                count = list.Count;
                Allocate(count);
                unsafe
                {
                    for (int i = 0; i < count; i++) {
                        Unsafe.Write(GetAddressInternal(i), list[i]);
                    }
                }
            }
        }

        public NativeArray(INative<T>[] array) {
            if (array != null) {
                count = array.Length;
                Allocate(count);
                unsafe
                {
                    for (int i = 0; i < count; i++) {
                        Unsafe.Write(GetAddressInternal(i), array[i].Native);
                    }
                }
            }
        }
        
        void Allocate(int count) {
            unsafe
            {
                if (count > 0) ptr = (void*)Marshal.AllocHGlobal(count * elementSize);
            }
            allocated = true;
        }

        public T this[int i] {
            get {
                if (i < 0 || i >= count) throw new IndexOutOfRangeException(string.Format("Index {0} is out of range [0, {1}]", i, count));
                unsafe
                {
                    return Unsafe.Read<T>(GetAddressInternal(i));
                }
            }
            set {
                if (i < 0 || i >= count) throw new IndexOutOfRangeException(string.Format("Index {0} is out of range [0, {1}]", i, count));
                unsafe
                {
                    Unsafe.Write(GetAddressInternal(i), value);
                }
            }
        }

        public IntPtr Address {
            get {
                unsafe
                {
                    return (IntPtr)ptr;
                }
            }
        }

        unsafe void* GetAddressInternal(int i) {
            return (void*)((byte*)ptr + (i * elementSize));
        }

        public IntPtr GetAddress(int i) {
            unsafe
            {
                return (IntPtr)GetAddressInternal(i);
            }
        }

        public int Count {
            get {
                return count;
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (allocated) {
                unsafe
                {
                    Marshal.FreeHGlobal((IntPtr)ptr);
                }
            }

            disposed = true;
        }

        ~NativeArray() {
            Dispose(false);
        }
    }
}
