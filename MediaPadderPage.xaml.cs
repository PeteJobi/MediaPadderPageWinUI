using DraggerResizer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.UI;
using WinUIShared.Controls;
using WinUIShared.Helpers;
using Orientation = DraggerResizer.Orientation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaPadderPage
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MediaPadderPage : Page
    {
        private readonly DraggerResizer.DraggerResizer contentResizer;
        private readonly DraggerResizer.DraggerResizer paddingResizer;
        private readonly PadMainModel viewModel;
        private FrameworkElement mediaElement;
        private readonly SolidColorBrush handleHoveredBrush = new(Color.FromArgb(255, 255, 255, 255));
        private readonly SolidColorBrush handlePressedHoverBrush = new(Color.FromArgb(150, 255, 255, 255));
        private string? navigateTo;
        private string? outputFile;
        private string ffmpegPath, mediaPath;
        private bool isVideo;
        private (string XText, string YText, string X2Text, string Y2Text) previousContentRect;
        private (string WidthText, string HeightText) previousPaddingSize;
        private readonly ObservableCollection<AspectRatio> ratios = [];
        private const double IconMaxSize = 40;
        private MediaPadderProcessor padProcessor;
        private HandlingParameters paddingHandlingParameters = new(){ Boundary = Boundary.NoBounds };

        public MediaPadderPage()
        {
            InitializeComponent();
            contentResizer = new DraggerResizer.DraggerResizer();
            paddingResizer = new DraggerResizer.DraggerResizer();
            viewModel = new PadMainModel();
            PopulateAspectRatios();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var props = (PadderProps)e.Parameter;
            ffmpegPath = props.FfmpegPath;
            mediaPath = props.MediaPath;
            padProcessor = new MediaPadderProcessor(ffmpegPath);
            var lowerCaseExt = mediaPath[^3..].ToLower();
            isVideo = lowerCaseExt is "mp4" or "mkv" or "mov";
            navigateTo = props.TypeToNavigateTo;
            HardwareSelector.SelectedGpu = props.Gpu;
            MediaName.Text = Path.GetFileName(mediaPath);
            if (isVideo)
            {
                Video.Source = MediaSource.CreateFromUri(new Uri(Processor.GetSafePath(mediaPath)));
                mediaElement = Video;
            }
            else
            {
                Image.Source = new BitmapImage(new Uri(Processor.GetSafePath(mediaPath)));
                mediaElement = Image;
            }
            base.OnNavigatedTo(e);
        }

        private static AspectRatio GetAspectRatio(double aspectWidth, double aspectHeight)
        {
            double width, height;
            if (aspectWidth > aspectHeight)
            {
                width = IconMaxSize;
                height = IconMaxSize * aspectHeight / aspectWidth;
            }
            else
            {
                height = IconMaxSize;
                width = IconMaxSize * aspectWidth / aspectHeight;
            }

            return new AspectRatio { Title = $"{aspectWidth}:{aspectHeight}", Width = width, Height = height };
        }

        private void PopulateAspectRatios()
        {
            ratios.Add(new AspectRatio { Title = "Square", Width = IconMaxSize, Height = IconMaxSize });
            ratios.Add(GetAspectRatio(16, 9));
            ratios.Add(GetAspectRatio(9, 16));
            ratios.Add(GetAspectRatio(5, 4));
            ratios.Add(GetAspectRatio(4, 5));
            ratios.Add(GetAspectRatio(4, 3));
            ratios.Add(GetAspectRatio(3, 4));
            ratios.Add(GetAspectRatio(3, 2));
            ratios.Add(GetAspectRatio(2, 3));
            ratios.Add(GetAspectRatio(2, 1));
            ratios.Add(GetAspectRatio(1, 2));
        }

        private void CanvasContainer_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CanvasContainer.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, CanvasContainer.ActualWidth, CanvasContainer.ActualHeight)
            };
            if (!double.IsNaN(mediaElement.Width)) CenterContentCanvas();
        }

        private void Video_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!double.IsNaN(mediaElement.Width)) return;
            SizeRetrieved(e.NewSize.Width, e.NewSize.Height);
        }

        private void Image_OnImageOpened(object sender, RoutedEventArgs e)
        {
            SizeRetrieved(Image.ActualWidth, Image.ActualHeight);
        }

        private void SizeRetrieved(double width, double height)
        {
            mediaElement.Width = width;
            mediaElement.Height = height;
            ContentCanvas.Width = mediaElement.Width;
            ContentCanvas.Height = mediaElement.Height;
            var contentOrientations = Enum.GetValues<Orientation>().Append(Orientation.Horizontal | Orientation.Vertical)
                .ToDictionary(o => o, o => new Appearance { HandleThickness = 30, Hover = handleHoveredBrush, Pressed = handlePressedHoverBrush });
            var paddingOrientations = new[] { Orientation.Left, Orientation.Top, Orientation.Right, Orientation.Bottom }
                .ToDictionary(o => o, o => new Appearance { HandleThickness = 30, Hover = handleHoveredBrush, Pressed = handlePressedHoverBrush });
            contentResizer.InitDraggerResizer(mediaElement, contentOrientations, GetContentHandlingParameters(), new HandlingCallbacks
            {
                BeforeDragging = point => new Point(point.X / ZoomTransform.ScaleX, point.Y / ZoomTransform.ScaleY),
                BeforeResizing = (point, _) => new Point(point.X / ZoomTransform.ScaleX, point.Y / ZoomTransform.ScaleY),
                AfterDragging = UpdateUiWithContentCoordinates,
                AfterResizing = (rect, _) => UpdateUiWithContentCoordinates(rect),
                DragCompleted = () =>
                {
                    var contentRect = GetCurrentContentRect();
                    contentResizer.PositionElement(mediaElement, double.Round(contentRect.Left), double.Round(contentRect.Top)); //Snap to whole pixels
                    ContentCoordinatesUpdated(positionChanged: true);
                },
                ResizeCompleted = _ =>
                {
                    contentResizer.ResizeElement(mediaElement, double.Round(mediaElement.Width), double.Round(mediaElement.Height)); //Snap to whole pixels
                    ContentCoordinatesUpdated(sizeChanged: true);
                }
            });
            paddingResizer.InitDraggerResizer(ContentCanvas, paddingOrientations, GetPaddingHandlingParameters(), new HandlingCallbacks
            {
                BeforeResizing = (point, _) => new Point(point.X / ZoomTransform.ScaleX, point.Y / ZoomTransform.ScaleY),
                AfterResizing = (_, _) => UpdateUiWithPaddingSize(GetCurrentPaddingSize()),
                ResizeCompleted = _ =>
                {
                    paddingResizer.ResizeElement(ContentCanvas, double.Round(ContentCanvas.Width), double.Round(ContentCanvas.Height)); //Snap to whole pixels
                    CenterContentCanvas();
                }
            });
            SetPaddingAspectRatio(1);
            CenterContentCanvas();
            contentResizer.PositionElementAtCenter(mediaElement);
            ContentCoordinatesUpdated();
            UpdateUiWithPaddingSize(GetCurrentPaddingSize());

            var originalAspectRatio = GetAspectRatio(width, height);
            originalAspectRatio.Title = "Original";
            ratios.Add(originalAspectRatio);
        }

        private void UpdateUiWithContentCoordinates(Rect newRect)
        {
            X.Text = newRect.X.ToString("F0");
            Y.Text = newRect.Y.ToString("F0");
            XDelta.Text = newRect.Width.ToString("F0");
            YDelta.Text = newRect.Height.ToString("F0");
            previousContentRect.XText = X.Text;
            previousContentRect.YText = Y.Text;
            previousContentRect.X2Text = XDelta.Text;
            previousContentRect.Y2Text = YDelta.Text;
        }

        private void UpdateUiWithPaddingSize(Size size)
        {
            OutputWidth.Text = size.Width.ToString("F0");
            OutputHeight.Text = size.Height.ToString("F0");
            previousPaddingSize.WidthText = OutputWidth.Text;
            previousPaddingSize.HeightText = OutputHeight.Text;
            if (isVideo) mediaElement.InvalidateMeasure(); //For some reason, canvas does not update ActualWidth/Height when resizing, so we have to force it. This only seems to be an issue with video, not images.
        }

        private void SetPaddingAspectRatio(double aspectRatio)
        {
            if (aspectRatio < mediaElement.Width / mediaElement.Height)
            {
                paddingResizer.ResizeElement(ContentCanvas, mediaElement.Width, mediaElement.Width / aspectRatio);
            }
            else
            {
                paddingResizer.ResizeElement(ContentCanvas, mediaElement.Height * aspectRatio, mediaElement.Height);
            }
            UpdateUiWithPaddingSize(new Size(ContentCanvas.Width, ContentCanvas.Height));
        }

        private void CenterContentCanvas()
        {
            var left = paddingResizer.GetElementLeft(ContentCanvas);
            var top = paddingResizer.GetElementTop(ContentCanvas);
            double panX, panY, zoom;
            if (CanvasContainer.ActualWidth / CanvasContainer.ActualHeight < ContentCanvas.Width / ContentCanvas.Height)
            {
                zoom = CanvasContainer.ActualWidth / ContentCanvas.Width; // Fit to width
                panY = (CanvasContainer.ActualHeight - zoom * ContentCanvas.Height) / 2 - top * zoom; // Center vertically
                panX = (CanvasContainer.ActualWidth - zoom * ContentCanvas.Width) / 2 - left * zoom;
            }
            else
            {
                zoom = CanvasContainer.ActualHeight / ContentCanvas.Height; // Fit to height
                panX = (CanvasContainer.ActualWidth - zoom * ContentCanvas.Width) / 2 - left * zoom; // Center horizontally
                panY = (CanvasContainer.ActualHeight - zoom * ContentCanvas.Height) / 2 - top * zoom;
            }
            AnimateTransform(panX, panY, zoom);
            if(CenterContent()) ContentCoordinatesUpdated();
        }

        private bool CenterContent()
        {
            if (LockToCenterCheckBox.IsChecked != true || contentResizer == null) return false;
                contentResizer.PositionElementAtCenter(mediaElement);
            return true;
            }

        private void AnimateTransform(double panX, double panY, double zoom)
        {
            var storyboard = new Storyboard(); //Animates PanTransform.X/Y and ZoomTransform.ScaleX/Y
            const double animDuration = 500;

            var animPanX = new DoubleAnimation
            {
                To = panX,
                Duration = new Duration(TimeSpan.FromMilliseconds(animDuration)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animPanX, PanTransform);
            Storyboard.SetTargetProperty(animPanX, "X");
            storyboard.Children.Add(animPanX);

            var animPanY = new DoubleAnimation
            {
                To = panY,
                Duration = new Duration(TimeSpan.FromMilliseconds(animDuration)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animPanY, PanTransform);
            Storyboard.SetTargetProperty(animPanY, "Y");
            storyboard.Children.Add(animPanY);

            var animZoomX = new DoubleAnimation
            {
                To = zoom,
                Duration = new Duration(TimeSpan.FromMilliseconds(animDuration)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animZoomX, ZoomTransform);
            Storyboard.SetTargetProperty(animZoomX, "ScaleX");
            storyboard.Children.Add(animZoomX);

            var animZoomY = new DoubleAnimation
            {
                To = zoom,
                Duration = new Duration(TimeSpan.FromMilliseconds(animDuration)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animZoomY, ZoomTransform);
            Storyboard.SetTargetProperty(animZoomY, "ScaleY");
            storyboard.Children.Add(animZoomY);

            storyboard.Begin();
        }

        private void UpdatePaddingHandlingParameters() =>
            paddingResizer.SetNewHandlingParameters(ContentCanvas, GetPaddingHandlingParameters());

        private HandlingParameters GetContentHandlingParameters() =>
            new()
            {
                KeepAspectRatio = LockContentAspectRatioCheckBox.IsChecked == true,
                Boundary = Boundary.BoundedAtEdges
            };

        private HandlingParameters GetPaddingHandlingParameters()
            {
            paddingHandlingParameters.KeepAspectRatio = LockPaddingAspectRatioCheckBox.IsChecked == true;
            var contentRect = GetCurrentContentRect();
            paddingHandlingParameters.MinimumWidth = (LockToCenterCheckBox.IsChecked == true ? 0 : contentRect.Left) + contentRect.Width;
            paddingHandlingParameters.MinimumHeight = (LockToCenterCheckBox.IsChecked == true ? 0 : contentRect.Top) + contentRect.Height;
            return paddingHandlingParameters;
        }

        private Rect GetCurrentContentRect() =>
            new(contentResizer.GetElementLeft(mediaElement), contentResizer.GetElementTop(mediaElement), mediaElement.Width, mediaElement.Height);

        private Size GetCurrentPaddingSize() => new(ContentCanvas.Width, ContentCanvas.Height);

        private void ContentCoordinatesUpdated(bool positionChanged = false, bool sizeChanged = false)
        {
            if (positionChanged) LockToCenterCheckBox.IsChecked = false;
            if(sizeChanged) CenterContent();
            UpdateUiWithContentCoordinates(GetCurrentContentRect());
            UpdatePaddingHandlingParameters();
        }

        private void X_OnTextChanged(object sender, RoutedEventArgs e)
        {
            if (previousContentRect.XText == X.Text) return;
            contentResizer.PositionElementLeft(mediaElement, X.Value);
            ContentCoordinatesUpdated(positionChanged: true);
        }

        private void XDelta_OnTextChanged(object sender, RoutedEventArgs e)
        {
            if (previousContentRect.X2Text == XDelta.Text) return;
            contentResizer.ResizeElementWidth(mediaElement, XDelta.Value);
            ContentCoordinatesUpdated(sizeChanged: true);
        }

        private void Y_OnTextChanged(object sender, RoutedEventArgs e)
        {
            if (previousContentRect.YText == Y.Text) return;
            contentResizer.PositionElementTop(mediaElement, Y.Value);
            ContentCoordinatesUpdated(positionChanged: true);
        }

        private void YDelta_OnTextChanged(object sender, RoutedEventArgs e)
        {
            if (previousContentRect.Y2Text == YDelta.Text) return;
            contentResizer.ResizeElementHeight(mediaElement, YDelta.Value);
            ContentCoordinatesUpdated(sizeChanged: true);
        }

        private void OutputWidth_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if(previousPaddingSize.WidthText == OutputWidth.Text) return;
            var contentRect = GetCurrentContentRect();
            var restrictedWidth = Math.Max((LockToCenterCheckBox.IsChecked == true ? 0 : contentRect.Left) + contentRect.Width, OutputWidth.Value);
            paddingResizer.ResizeElementWidth(ContentCanvas, restrictedWidth, parameters: GetPaddingHandlingParameters());
            CenterContentCanvas();
            UpdateUiWithPaddingSize(GetCurrentPaddingSize());
        }

        private void OutputHeight_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if(previousPaddingSize.HeightText == OutputHeight.Text) return;
            var contentRect = GetCurrentContentRect();
            var restrictedHeight = Math.Max((LockToCenterCheckBox.IsChecked == true ? 0 : contentRect.Top) + contentRect.Height, OutputHeight.Value);
            paddingResizer.ResizeElementHeight(ContentCanvas, restrictedHeight, parameters: GetPaddingHandlingParameters()); 
            CenterContentCanvas();
            UpdateUiWithPaddingSize(GetCurrentPaddingSize());
        }

        private void LockContentAspectRatioChanged(object sender, RoutedEventArgs e)
        {
            contentResizer?.SetNewHandlingParameters(mediaElement, GetContentHandlingParameters());
        }

        private void LockToCenterChanged(object sender, RoutedEventArgs e)
        {
            if(CenterContent()) ContentCoordinatesUpdated();
            else if (contentResizer != null) UpdatePaddingHandlingParameters();
        }

        private void LockPaddingAspectRatioChanged(object sender, RoutedEventArgs e)
        {
            paddingResizer.SetNewHandlingParameters(ContentCanvas, GetPaddingHandlingParameters());
        }

        private void SpecificRatio(object sender, RoutedEventArgs e)
        {
            var ratio = (AspectRatio)((Button)sender).DataContext;
            SetPaddingAspectRatio(ratio.Width / ratio.Height);
            CenterContentCanvas();
        }

        private async void Pad(object sender, RoutedEventArgs e)
        {
            outputFile = null;
            outputFile = await ProcessManager.StartProcess(padProcessor.PadMedia(mediaPath, GetCurrentContentRect(), GetCurrentPaddingSize(), "#000000", !isVideo));
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            if (isVideo) Video.MediaPlayer.Pause();
            _ = padProcessor.Cancel();
            if (navigateTo == null) Frame.GoBack();
            else Frame.NavigateToType(Type.GetType(navigateTo), outputFile, new FrameNavigationOptions { IsNavigationStackEnabled = false });
        }
    }

    class AspectRatio
    {
        public string Title { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class PadderProps
    {
        public string FfmpegPath { get; set; }
        public string MediaPath { get; set; }
        public string? TypeToNavigateTo { get; set; }
        public GpuInfo? Gpu { get; set; }
    }
}
