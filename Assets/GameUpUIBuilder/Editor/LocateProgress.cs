using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Trạng thái trực tiếp của một lần định vị, dựng từ các dòng <c>@progress {json}</c> của <c>ui_locate.py</c>:
    /// giai đoạn hiện tại, tiến độ, nhật ký và <see cref="Snapshot"/> — kết quả tạm để vẽ khung lên ảnh demo khi chưa xong.
    /// </summary>
    public sealed class LocateProgress
    {
        public const string LinePrefix = "@progress ";
        private const int MaxEvents = 300;

        public static readonly string[] Stages = { "Chuẩn bị", "Dò sprite", "Lọc chéo", "Tìm & đọc chữ" };

        private readonly List<string> _events = new List<string>();

        public int Stage { get; private set; }

        public int Done { get; private set; }

        public int Total { get; private set; }

        public int CachedCount { get; private set; }

        public int Workers { get; private set; }

        /// <summary>Việc đang làm, một dòng.</summary>
        public string Status { get; private set; } = "Nạp ảnh demo, art và cache…";

        /// <summary>Sprite vừa dò xong — preview tô nổi để thấy máy vừa tìm được gì.</summary>
        public string LatestSprite { get; private set; }

        /// <summary>Kết quả tạm: sprite khớp, chữ và vị trí bị lọc tới thời điểm hiện tại.</summary>
        public LocateResult Snapshot { get; } = new LocateResult();

        public IReadOnlyList<string> Events => _events;

        /// <summary>Đọc một dòng stdout; bỏ qua dòng không phải sự kiện tiến trình.</summary>
        public void ApplyLine(string line)
        {
            if (!line.StartsWith(LinePrefix, StringComparison.Ordinal)) return;
            LocateProgressEvent e;
            try
            {
                e = JsonUtility.FromJson<LocateProgressEvent>(line.Substring(LinePrefix.Length));
            }
            catch (ArgumentException)
            {
                return;
            }

            if (e != null) Apply(e);
        }

        private void Apply(LocateProgressEvent e)
        {
            switch (e.phase)
            {
                case LocateProgressEvent.PhaseStart:
                    ApplyStart(e);
                    break;
                case LocateProgressEvent.PhaseSprite:
                    ApplySprite(e);
                    break;
                case LocateProgressEvent.PhaseFilter:
                    Stage = 2;
                    Status = $"Lọc chéo: {e.message}";
                    break;
                case LocateProgressEvent.PhaseDrop:
                    ApplyDrop(e.drop);
                    break;
                case LocateProgressEvent.PhaseTextScan:
                case LocateProgressEvent.PhaseTextRead:
                    Stage = 3;
                    Status = e.message;
                    if (e.total > 0) SetCount(e.done, e.total);
                    break;
                case LocateProgressEvent.PhaseText:
                    ApplyText(e);
                    break;
            }
        }

        private void ApplyStart(LocateProgressEvent e)
        {
            Stage = 1;
            Total = e.total;
            CachedCount = e.cachedCount;
            Workers = e.workers;
            Snapshot.demoWidth = e.demoWidth;
            Snapshot.demoHeight = e.demoHeight;
            Status = $"Dò {e.total} sprite ({e.cachedCount} lấy từ cache) trên {e.workers} process";
            AddEvent($"▶ {Status}");
        }

        private void ApplySprite(LocateProgressEvent e)
        {
            var sprite = e.sprite;
            if (sprite == null) return;
            Stage = 1;
            SetCount(e.done, e.total);
            Snapshot.sprites.Add(sprite);
            var source = e.cached ? " (cache)" : string.Empty;
            if (sprite.IsMatched)
            {
                LatestSprite = sprite.name;
                var first = sprite.matches[0];
                var flat = sprite.lowTexture ? " · một màu, chốt theo hình dáng" : string.Empty;
                Status = $"Khớp {sprite.name}";
                AddEvent($"✔ {sprite.name} — {sprite.matches.Count} chỗ · {first.MethodLabel}{flat} · {first.ScoreLabel}{source}");
            }
            else
            {
                // Không ghi nhật ký: thư mục art lớn có hàng trăm sprite không thuộc màn này (xem mục "Không khớp").
                Status = $"Không khớp {sprite.name}";
            }
        }

        private void ApplyDrop(LocateDrop drop)
        {
            if (drop == null) return;
            Snapshot.dropped.Add(drop);
            var owner = Snapshot.sprites.FirstOrDefault(s => s.name == drop.name && s.IsMatched);
            owner?.matches.RemoveAll(m => m.x == drop.x && m.y == drop.y && m.w == drop.w && m.h == drop.h);
            AddEvent($"✕ {drop.name} @({drop.x},{drop.y}) — {drop.ReasonLabel}");
        }

        private void ApplyText(LocateProgressEvent e)
        {
            var text = e.text;
            if (text == null) return;
            Stage = 3;
            if (e.total > 0) SetCount(e.done, e.total);
            Snapshot.texts.Add(text);
            var content = string.IsNullOrEmpty(text.text) ? "(chưa đọc nội dung)" : $"“{text.text}” {text.confidence:P0}";
            AddEvent($"T {content} · {text.w}×{text.h} @({text.x},{text.y})");
        }

        private void SetCount(int done, int total)
        {
            Done = done;
            Total = total;
        }

        private void AddEvent(string text)
        {
            _events.Add(text);
            if (_events.Count > MaxEvents) _events.RemoveAt(0);
        }
    }
}
