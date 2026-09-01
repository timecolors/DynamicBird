using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using QRCoder;
using ZXing;
using ZXing.Common;

// 生成应用图标用的二维码，并用 ZXing 回读校验可扫性。
// 用法：QrGen <text/url> <output.png> [pixels]
if (args.Length < 2)
{
    Console.WriteLine("用法: QrGen <内容> <输出.png> [边长]");
    return 2;
}

string content = args[0];
string output = args[1];
int size = args.Length > 2 && int.TryParse(args[2], out var s) ? s : 1024;
bool noQuiet = args.Length > 3 && args[3] == "-noquiet";

using var gen = new QRCodeGenerator();
// 高纠错等级 H：允许图标中央放 logo 后仍可扫描
var data = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.H);
int modules = data.ModuleMatrix.Count;
int ppm = Math.Max(4, noQuiet ? size / modules : size / (modules + 8)); // -noquiet 去掉静区
using var qr = new QRCode(data);
using var bmp = qr.GetGraphic(ppm, "#1E1E1E", "#FFFFFF", !noQuiet);

// 校验：ZXing 回读解码
using var ms = new MemoryStream();
bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
ms.Position = 0;
using (var bmp32 = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb))
{
    using (var g = Graphics.FromImage(bmp32))
        g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
    var pixels = new byte[bmp32.Width * bmp32.Height * 4];
    var bd = bmp32.LockBits(new Rectangle(0, 0, bmp32.Width, bmp32.Height),
        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(bd.Scan0, pixels, 0, pixels.Length);
    bmp32.UnlockBits(bd);
    var src = new RGBLuminanceSource(pixels, bmp32.Width, bmp32.Height,
        RGBLuminanceSource.BitmapFormat.BGRA32);
    var reader = new MultiFormatReader();
    var result = reader.decode(new BinaryBitmap(new HybridBinarizer(src)));
    bool ok = result != null && string.Equals(result.Text, content, StringComparison.Ordinal);
    Console.WriteLine($"解码校验: {(ok ? "通过" : "失败")} (内容长度 {content.Length})");
    if (!ok) return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
bmp.Save(output, System.Drawing.Imaging.ImageFormat.Png);
Console.WriteLine($"已生成: {Path.GetFullPath(output)} ({size}x{size})");
return 0;
