using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using WinUIShared.Controls;
using WinUIShared.Helpers;

namespace MediaPadderPage
{
    public class MediaPadderProcessor(string ffmpegPath) : Processor(ffmpegPath, new FileLogger.FileLogger("ReelBox/Pad"))
    {
        public async Task PadMedia(string inputPath, Rect inputRect, Size outputSize, string colour, bool isImage)
        {
            progressPrimary.Report(0);
            centerTextPrimary.Report("0.0 %");
            rightTextPrimary.Report("Padding...");

            var scalePadParams = $"scale={inputRect.Width}:{inputRect.Height},pad={outputSize.Width}:{outputSize.Height}:{inputRect.X}:{inputRect.Y}:{colour}";
            if (isImage)
            {
                await StartFfmpegProcess($"-i \"{inputPath}\" -vf \"{scalePadParams},setsar=1\" \"{GetOutputName(inputPath)}\"", ProgressHandler); // Images do not support hardware acceleration. (they do, but it is not worth the complexity)
            }
            else
            {
                switch (gpuInfo?.Vendor)
                {
                    case GpuVendor.Nvidia:
                        scalePadParams = $"scale_cuda={inputRect.Width}:{inputRect.Height},pad_cuda={outputSize.Width}:{outputSize.Height}:{inputRect.X}:{inputRect.Y}:{colour}";
                        break;
                    case GpuVendor.Amd or GpuVendor.Intel:
                        var gpuPixelFormat = await GetGpuPixelFormat(inputPath);
                        var (hwDownArgs, hwUpArgs) = GpuInfo.FilterParams(gpuInfo, gpuPixelFormat);
                        scalePadParams = $"{hwDownArgs}{scalePadParams}{hwUpArgs}";
                        break;
                }
                await StartFfmpegTranscodingProcessDefaultQuality([inputPath], GetOutputName(inputPath), $"-vf \"{scalePadParams},setsar=1\"", ProgressHandler);
            }
            if (HasBeenKilled()) return;
            AllDone();

            void ProgressHandler(double progress, TimeSpan currentTime, TimeSpan duration, int fps)
            {
                progressPrimary.Report(progress);
                centerTextPrimary.Report($"{Math.Round(progress, 2)} %");
            }
        }

        private string GetOutputName(string path)
        {
            var inputName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var parentFolder = Path.GetDirectoryName(path) ?? throw new FileNotFoundException($"The specified path does not exist: {path}");
            outputFile = Path.Combine(parentFolder, $"{inputName}_PADDED{extension}");
            File.Delete(outputFile);
            return outputFile;
        }

        private void AllDone()
        {
            progressPrimary.Report(ProgressMax);
            centerTextPrimary.Report("100 %");
        }
    }
}
