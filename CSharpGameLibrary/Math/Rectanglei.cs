﻿using System;
using System.Numerics;

namespace CSGL.Math {
    public struct Rectanglei {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectanglei(int x, int y, int width, int height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rectanglei(Rectangle rect) {
            X = (int)rect.X;
            Y = (int)rect.Y;
            Width = (int)rect.Width;
            Height = (int)rect.Height;
        }

        //public Vector2i

        public float Top {
            get {
                return Y;
            }
        }

        public float Bottom {
            get {
                return Y + Height;
            }
        }

        public float Left {
            get {
                return X;
            }
        }

        public float Right {
            get {
                return X + Width;
            }
        }

        public bool Intersects(Rectanglei other) {
            return (X < other.X + other.Width) &&
                (X + Width > other.X) &&
                (Y < other.Y + other.Height) &&
                (Y + Height > other.Y);
        }

        public bool Intersects(Rectangle other) {
            var thisf = new Rectangle(this);
            return thisf.Intersects(other);
        }
    }
}