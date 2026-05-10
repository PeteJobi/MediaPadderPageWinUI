## Media Padder Page (WinUI 3)
This provides a reuseable WinUI 3 page with an interface that allows for padding the dimensions of videos and images.

<img width="1786" height="1075" alt="image" src="https://github.com/user-attachments/assets/7161a45f-c8bb-4f61-bc97-0480aa65d382" />

# How to use
This library depends on [DraggerResizerWinUI](https://github.com/PeteJobi/DraggerResizerWinUI), and for videos, [TextToTimeSpanWinUI](https://github.com/PeteJobi/TextToTimeSpanWinUI) and [TimelineWinUI](https://github.com/PeteJobi/TimelineWinUI). Include the libraries into your WinUI solution and reference them in your WinUI project. Then navigate to the **MediaPadderPage** when the user requests for it, passing a **PadderProps** object as parameter.
The **PadderProps** object should contain the path to ffmpeg, the path to the media file, and optionally, the full name of the Page type to navigate back to when the user is done. If this last parameter is provided, you can get the path to the file that was generated on the Media Padder page. If not, the user will be navigated back to whichever page called the Media Padder page and there'll be no parameters. 
```
private void GoToPadder(){
  var ffmpegPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/ffmpeg.exe");
  var mediaPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/image.png");
  Frame.Navigate(typeof(MediaPadderPage), new PadderProps { FfmpegPath = ffmpegPath, MediaPath = mediaPath, TypeToNavigateTo = typeof(MainPage).FullName });
}

protected override void OnNavigatedTo(NavigationEventArgs e)
{
    //outputFile is sent only if TypeToNavigateTo was specified in TourProps.
    if (e.Parameter is string outputFile)
    {
        Console.WriteLine($"Path to the toured file is {outputFile}");
    }
}
```

You may check out [MediaPadder](https://github.com/PeteJobi/MediaPadder) to see a full application that uses this page.
