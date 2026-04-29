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
            var isOnlyScaling = inputRect is { X: 0, Y: 0 } && Math.Abs(inputRect.Width - outputSize.Width) < 1 && Math.Abs(inputRect.Height - outputSize.Height) < 1;
            progressPrimary.Report(0);
            centerTextPrimary.Report("0.0 %");
            rightTextPrimary.Report(isOnlyScaling ? "Scaling..." : "Padding...");

            var scalePadParams = $"scale={outputSize.Width}:{outputSize.Height}";
            if(!isOnlyScaling) scalePadParams += $",pad={outputSize.Width}:{outputSize.Height}:{inputRect.X}:{inputRect.Y}:{colour}";
            if (isImage)
            {
                await StartFfmpegProcess($"-i \"{inputPath}\" -vf \"{scalePadParams},setsar=1\" \"{GetOutputName(inputPath)}\"", ProgressHandler); // Images do not support hardware acceleration. (they do, but it is not worth the complexity)
            }
            else
            {
                var gpuPixelFormat = await GetGpuPixelFormat(inputPath);
                switch (gpuInfo?.Vendor)
                {
                    case GpuVendor.Nvidia:
                        if(isOnlyScaling) scalePadParams = $"scale_cuda={outputSize.Width}:{outputSize.Height}";
                        else if (gpuPixelFormat == "nv12") //pad_cuda only works on 8-bit videos
                        {
                            scalePadParams = $"scale_cuda={inputRect.Width}:{inputRect.Height},pad_cuda={outputSize.Width}:{outputSize.Height}:{inputRect.X}:{inputRect.Y}:{colour}";
                        }
                        else //use plain CPU pad filter
                        {
                            var (hwDownArgs, hwUpArgs) = GpuInfo.FilterParams(gpuInfo, gpuPixelFormat);
                            scalePadParams = $"{hwDownArgs}{scalePadParams}{hwUpArgs}";
                        }
                        break;
                    case GpuVendor.Amd:
                        {
                            var (hwDownArgs, hwUpArgs) = GpuInfo.FilterParams(gpuInfo, gpuPixelFormat);
                            scalePadParams = $"{hwDownArgs}{scalePadParams}{hwUpArgs}";
                        }
                        break;
                    case GpuVendor.Intel:
                        if(isOnlyScaling) scalePadParams = $"scale_qsv={outputSize.Width}:{outputSize.Height}";
                        else
                        {
                            var (hwDownArgs, hwUpArgs) = GpuInfo.FilterParams(gpuInfo, gpuPixelFormat);
                            scalePadParams = $"{hwDownArgs}{scalePadParams}{hwUpArgs}";
                        }
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
