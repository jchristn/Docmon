namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Enums;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Lists local images with their local and registry digests and the resulting update status.
    /// </summary>
    public sealed class ImagesScreen : DocmonScreen
    {
        private static readonly string[] _Headers = { "REPOSITORY", "TAG", "LOCAL", "REMOTE", "STATUS", "SIZE" };
        private static readonly int[] _Weights = { 8, 4, 4, 4, 4, 3 };

        private readonly TableState _Table = new TableState();
        private int _Count;

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "↑↓ select · u recheck updates · p pull selected · P pull new image · d delete · x prune dangling"; }
        }

        /// <inheritdoc/>
        public override object? SelectedTag
        {
            get { return _Table.SelectedTag; }
        }

        /// <summary>
        /// Updates the image list.
        /// </summary>
        /// <param name="images">The images. Must not be null.</param>
        public void SetImages(IReadOnlyList<ImageInfo> images)
        {
            if (images == null) throw new ArgumentNullException(nameof(images));

            _Count = images.Count;
            List<RowItem> rows = new List<RowItem>(images.Count);
            foreach (ImageInfo image in images)
            {
                string[] cells =
                {
                    image.Repository,
                    image.Tag,
                    ShortDigest(FirstLocalDigest(image)),
                    ShortDigest(image.RemoteDigest),
                    StatusLabel(image.Status),
                    ByteFormatter.Format(image.SizeBytes)
                };
                rows.Add(new RowItem(cells, image, StatusColors.ForUpdate(image.Status)));
            }

            _Table.SetRows(rows);
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            return _Table.HandleKey(key);
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 4)
                return;

            CellStyle border = CellStyle.Default.WithForeground(Focused ? DocmonPalette.Accent : DocmonPalette.Dim);
            Rect inner = Draw.Panel(surface, new Rect(0, 0, width, height), "Images (" + _Count + ")", border);
            _Table.Render(surface, inner, _Headers, _Weights, Focused, "No images found.");
        }

        private static string FirstLocalDigest(ImageInfo image)
        {
            if (image.RepoDigests.Count == 0)
                return string.Empty;

            string first = image.RepoDigests[0];
            int at = first.IndexOf('@');
            return at >= 0 ? first.Substring(at + 1) : first;
        }

        private static string ShortDigest(string? digest)
        {
            if (string.IsNullOrEmpty(digest))
                return "-";

            int colon = digest.IndexOf(':');
            string hex = colon >= 0 ? digest.Substring(colon + 1) : digest;
            return hex.Length > 10 ? hex.Substring(0, 10) : hex;
        }

        private static string StatusLabel(UpdateStatusEnum status)
        {
            switch (status)
            {
                case UpdateStatusEnum.Current: return "current";
                case UpdateStatusEnum.UpdateAvailable: return "update ⇧";
                case UpdateStatusEnum.Checking: return "checking";
                case UpdateStatusEnum.Error: return "error";
                default: return "-";
            }
        }
    }
}
