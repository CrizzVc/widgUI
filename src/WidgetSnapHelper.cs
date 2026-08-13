using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WidgUI
{
    public static class WidgetSnapHelper
    {
        private const double SnapThreshold = 10;

        private static Window _draggingWindow;
        private static Point _dragStartScreen;
        private static double _windowStartLeft;
        private static double _windowStartTop;
        private static WidgetAlignmentOverlay _overlay;

        public static void BeginSnapDrag(Window window, MouseButtonEventArgs e)
        {
            if (window == null || e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            StartDrag(window, window.PointToScreen(e.GetPosition(window)));
        }

        public static void BeginSnapDrag(Window window, MouseEventArgs e)
        {
            if (window == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            StartDrag(window, window.PointToScreen(e.GetPosition(window)));
        }

        private static void StartDrag(Window window, Point screenDragPoint)
        {
            if (_draggingWindow != null)
            {
                return;
            }

            _draggingWindow = window;
            _dragStartScreen = screenDragPoint;
            _windowStartLeft = window.Left;
            _windowStartTop = window.Top;

            WidgetRegistry.BeginTemporaryLayerBoost(window);

            EnsureOverlay();
            window.CaptureMouse();
            window.MouseMove += Window_MouseMove;
            window.MouseLeftButtonUp += Window_MouseLeftButtonUp;
            window.LostMouseCapture += Window_LostMouseCapture;
        }

        private static void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingWindow == null || e.LeftButton != MouseButtonState.Pressed)
            {
                EndDrag();
                return;
            }

            Point currentScreen = _draggingWindow.PointToScreen(e.GetPosition(_draggingWindow));
            double deltaX = currentScreen.X - _dragStartScreen.X;
            double deltaY = currentScreen.Y - _dragStartScreen.Y;

            double proposedLeft = _windowStartLeft + deltaX;
            double proposedTop = _windowStartTop + deltaY;

            SnapResult snap = CalculateSnap(_draggingWindow, proposedLeft, proposedTop);
            _draggingWindow.Left = snap.Left;
            _draggingWindow.Top = snap.Top;

            if (_overlay != null)
            {
                _overlay.ShowGuides(snap.VerticalGuides, snap.HorizontalGuides);
            }
        }

        private static void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }

        private static void Window_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_draggingWindow == sender)
            {
                EndDrag();
            }
        }

        private static void EndDrag()
        {
            if (_draggingWindow == null)
            {
                return;
            }

            Window window = _draggingWindow;
            _draggingWindow = null;

            window.MouseMove -= Window_MouseMove;
            window.MouseLeftButtonUp -= Window_MouseLeftButtonUp;
            window.LostMouseCapture -= Window_LostMouseCapture;

            if (window.IsMouseCaptured)
            {
                window.ReleaseMouseCapture();
            }

            if (_overlay != null)
            {
                _overlay.HideGuides();
            }

            WidgetRegistry.EndTemporaryLayerBoost(window);
            WidgetRegistry.EnsureEdgeMenuOnTop();
            WidgetRegistry.AutoSaveLayout();
        }

        private static void EnsureOverlay()
        {
            if (_overlay == null)
            {
                _overlay = new WidgetAlignmentOverlay();
            }
        }

        private static SnapResult CalculateSnap(Window movingWindow, double proposedLeft, double proposedTop)
        {
            double width = movingWindow.ActualWidth > 0 ? movingWindow.ActualWidth : movingWindow.Width;
            double height = movingWindow.ActualHeight > 0 ? movingWindow.ActualHeight : movingWindow.Height;

            List<double> targetXs = new List<double>();
            List<double> targetYs = new List<double>();

            foreach (Rect bounds in WidgetRegistry.GetWidgetBoundsExcept(movingWindow))
            {
                targetXs.Add(bounds.Left);
                targetXs.Add(bounds.Left + bounds.Width / 2.0);
                targetXs.Add(bounds.Right);

                targetYs.Add(bounds.Top);
                targetYs.Add(bounds.Top + bounds.Height / 2.0);
                targetYs.Add(bounds.Bottom);
            }

            double movingLeft = proposedLeft;
            double movingCenterX = proposedLeft + width / 2.0;
            double movingRight = proposedLeft + width;
            double movingTop = proposedTop;
            double movingCenterY = proposedTop + height / 2.0;
            double movingBottom = proposedTop + height;

            double bestXDistance = SnapThreshold;
            double xAdjust = 0;
            double? guideX = null;

            foreach (double targetX in targetXs)
            {
                TrySnapEdge(movingLeft, targetX, ref bestXDistance, ref xAdjust, ref guideX);
                TrySnapEdge(movingCenterX, targetX, ref bestXDistance, ref xAdjust, ref guideX);
                TrySnapEdge(movingRight, targetX, ref bestXDistance, ref xAdjust, ref guideX);
            }

            double bestYDistance = SnapThreshold;
            double yAdjust = 0;
            double? guideY = null;

            foreach (double targetY in targetYs)
            {
                TrySnapEdge(movingTop, targetY, ref bestYDistance, ref yAdjust, ref guideY);
                TrySnapEdge(movingCenterY, targetY, ref bestYDistance, ref yAdjust, ref guideY);
                TrySnapEdge(movingBottom, targetY, ref bestYDistance, ref yAdjust, ref guideY);
            }

            SnapResult result;
            result.Left = proposedLeft + xAdjust;
            result.Top = proposedTop + yAdjust;
            result.VerticalGuides = new List<double>();
            result.HorizontalGuides = new List<double>();

            if (guideX.HasValue)
            {
                result.VerticalGuides.Add(guideX.Value);
            }

            if (guideY.HasValue)
            {
                result.HorizontalGuides.Add(guideY.Value);
            }

            return result;
        }

        private static void TrySnapEdge(double movingEdge, double targetLine,
            ref double bestDistance, ref double adjust, ref double? guide)
        {
            double distance = Math.Abs(movingEdge - targetLine);
            if (distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            adjust = targetLine - movingEdge;
            guide = targetLine;
        }

        private struct SnapResult
        {
            public double Left;
            public double Top;
            public List<double> VerticalGuides;
            public List<double> HorizontalGuides;
        }

        private sealed class WidgetAlignmentOverlay : Window
        {
            private readonly Canvas _canvas;
            private readonly SolidColorBrush _guideBrush;

            public WidgetAlignmentOverlay()
            {
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                ShowInTaskbar = false;
                IsHitTestVisible = false;
                Topmost = true;

                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;

                _guideBrush = new SolidColorBrush(Color.FromArgb(210, 0, 196, 255));

                _canvas = new Canvas
                {
                    Width = Width,
                    Height = Height
                };

                Content = _canvas;
            }

            public void ShowGuides(List<double> verticalGuides, List<double> horizontalGuides)
            {
                _canvas.Children.Clear();

                foreach (double screenX in verticalGuides)
                {
                    double x = screenX - Left;
                    Line line = new Line
                    {
                        X1 = x,
                        X2 = x,
                        Y1 = 0,
                        Y2 = Height,
                        Stroke = _guideBrush,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 3 }
                    };
                    _canvas.Children.Add(line);
                }

                foreach (double screenY in horizontalGuides)
                {
                    double y = screenY - Top;
                    Line line = new Line
                    {
                        X1 = 0,
                        X2 = Width,
                        Y1 = y,
                        Y2 = y,
                        Stroke = _guideBrush,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 3 }
                    };
                    _canvas.Children.Add(line);
                }

                if (!IsVisible)
                {
                    Show();
                }
            }

            public void HideGuides()
            {
                _canvas.Children.Clear();
                Hide();
            }
        }
    }
}
