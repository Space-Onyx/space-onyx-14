using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using System.Numerics;

namespace Content.Client._Onyx.Telecommunications;

public sealed class TelecomTrafficGraph : Control
{
    private static readonly Color Background = Color.FromHex("#06100c");
    private static readonly Color Grid = Color.FromHex("#17372b");
    private static readonly Color Axis = Color.FromHex("#6edc9b");
    private static readonly Color Received = Color.FromHex("#58c7ff");
    private static readonly Color Sent = Color.FromHex("#45e67b");
    private static readonly Color Failed = Color.FromHex("#e65454");
    private static readonly Color BinSeparator = Color.FromHex("#0d2a1a");

    private int[] _received = [];
    private int[] _sent = [];
    private int[] _receivedBytes = [];
    private int[] _sentBytes = [];

    public TelecomTrafficGraph()
    {
        MinSize = new Vector2(0, 210);
        HorizontalExpand = true;
    }

    public void SetTraffic(int[] received, int[] sent, int[] receivedBytes, int[] sentBytes)
    {
        _received = received;
        _sent = sent;
        _receivedBytes = receivedBytes;
        _sentBytes = sentBytes;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), Background);

        var center = PixelHeight / 2f;
        var padding = 10f;
        var graphHeight = Math.Max(1f, center - padding);

        for (var i = 1; i < 4; i++)
        {
            var offset = graphHeight * i / 4f;
            handle.DrawLine(new Vector2(0, center - offset), new Vector2(PixelWidth, center - offset), Grid);
            handle.DrawLine(new Vector2(0, center + offset), new Vector2(PixelWidth, center + offset), Grid);
        }

        handle.DrawLine(new Vector2(0, center), new Vector2(PixelWidth, center), Axis);

        var count = Math.Min(_received.Length, _sent.Length);
        if (count == 0)
            return;

        var maxBytes = 1;
        for (var i = 0; i < count; i++)
        {
            maxBytes = Math.Max(maxBytes, Math.Max(_receivedBytes[i], _sentBytes[i]));
        }

        var cellWidth = PixelWidth / (float) count;
        var binGap = Math.Clamp(cellWidth * 0.08f, 1f, 3f);
        var barGap = Math.Clamp(cellWidth * 0.06f, 1f, 2f);
        var barWidth = Math.Max(1f, (cellWidth - binGap - barGap * 2f) / 2f);

        for (var i = 0; i < count; i++)
        {
            var binLeft = i * cellWidth;
            var binRight = (i + 1) * cellWidth;

            if (i > 0)
            {
                handle.DrawRect(
                    new UIBox2(binLeft, 0, binLeft + binGap, PixelHeight),
                    BinSeparator);
            }

            var contentLeft = binLeft + binGap + barGap;
            var receivedLeft = contentLeft;
            var receivedRight = contentLeft + barWidth;
            var sentLeft = contentLeft + barWidth + barGap;
            var sentRight = contentLeft + barWidth * 2f + barGap;

            var maxLog = Math.Log(1 + maxBytes);
            var receivedHeight = _receivedBytes[i] > 0
                ? (float)(Math.Log(1 + _receivedBytes[i]) / maxLog * (graphHeight - 3f))
                : 0f;
            var sentHeight = _sentBytes[i] > 0
                ? (float)(Math.Log(1 + _sentBytes[i]) / maxLog * (graphHeight - 3f))
                : 0f;

            var failedBytes = Math.Max(0, _receivedBytes[i] - _sentBytes[i]);
            var failedHeight = failedBytes > 0
                ? (float)(Math.Log(1 + failedBytes) / maxLog * (graphHeight - 3f))
                : 0f;

            if (receivedHeight > 0)
                handle.DrawRect(new UIBox2(receivedLeft, center - receivedHeight, receivedRight, center - 2f), Received);

            if (failedHeight > 0)
                handle.DrawRect(new UIBox2(receivedLeft, center - receivedHeight, receivedRight, center - receivedHeight + failedHeight), Failed);

            if (sentHeight > 0)
                handle.DrawRect(new UIBox2(sentLeft, center + 2f, sentRight, center + sentHeight), Sent);
        }
    }
}
