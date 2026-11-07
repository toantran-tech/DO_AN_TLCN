using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace WashGo.Application.Features.FileApplication.Services
{
    /// <summary>Vị trí lưu ảnh upload và kiểm tra URL ảnh mà client gửi lại.</summary>
    public static class UploadStorage
    {
        public const string RequestPath = "/uploads";
        public const long MaxImageBytes = 5 * 1024 * 1024;
        public const int MaxImagesPerSubmission = 5;
        public const long MaxVideoBytes = 50 * 1024 * 1024;
        public const int MaxVideosPerSubmission = 2;

        private static readonly Regex UrlPattern =
            new(@"^/uploads/(?<owner>[0-9a-f]{32})/(?<file>[0-9a-f]{32}\.(jpg|png|webp))$", RegexOptions.Compiled);

        private static readonly Regex VideoUrlPattern =
            new(@"^/uploads/(?<owner>[0-9a-f]{32})/(?<file>[0-9a-f]{32}\.(mp4|webm|mov))$", RegexOptions.Compiled);

        public static string GetRoot(IConfiguration configuration) =>
            Path.GetFullPath(configuration["Storage:UploadPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads"));

        /// <summary>
        /// Chỉ chấp nhận ảnh do chính người dùng upload và còn tồn tại, để không gắn URL tùy ý hoặc ảnh của người khác.
        /// </summary>
        public static string? ValidateOwnedImages(IConfiguration configuration, IEnumerable<string>? urls, Guid ownerId,
            int minCount, out List<string> normalized)
        {
            normalized = (urls ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (normalized.Count < minCount)
                return "Vui lòng upload ít nhất 1 ảnh minh chứng.";
            if (normalized.Count > MaxImagesPerSubmission)
                return $"Tối đa {MaxImagesPerSubmission} ảnh.";
            var root = GetRoot(configuration);
            var owner = ownerId.ToString("N");
            foreach (var url in normalized)
            {
                var match = UrlPattern.Match(url);
                if (!match.Success || match.Groups["owner"].Value != owner ||
                    !File.Exists(Path.Combine(root, owner, match.Groups["file"].Value)))
                    return "Ảnh đính kèm không hợp lệ; vui lòng upload lại.";
            }
            return null;
        }

        /// <summary>Như ảnh: chỉ nhận video do chính người dùng upload qua /files/videos và còn tồn tại.</summary>
        public static string? ValidateOwnedVideos(IConfiguration configuration, IEnumerable<string>? urls, Guid ownerId,
            out List<string> normalized)
        {
            normalized = (urls ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (normalized.Count > MaxVideosPerSubmission)
                return $"Tối đa {MaxVideosPerSubmission} video.";
            var root = GetRoot(configuration);
            var owner = ownerId.ToString("N");
            foreach (var url in normalized)
            {
                var match = VideoUrlPattern.Match(url);
                if (!match.Success || match.Groups["owner"].Value != owner ||
                    !File.Exists(Path.Combine(root, owner, match.Groups["file"].Value)))
                    return "Video đính kèm không hợp lệ; vui lòng upload lại.";
            }
            return null;
        }
    }
}
