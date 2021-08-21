﻿using Admin.Core.Common.Attributes;
using Admin.Core.Common.Cache;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace Admin.Tools.Captcha
{
    /// <summary>
    /// 滑块拼图验证
    /// </summary>
    [SingleInstance]
    public class SlideJigsawCaptcha : ICaptcha
    {
        private readonly ICache _cache;

        public SlideJigsawCaptcha(ICache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Bitmap转为base64编码的文本
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private string ImgToBase64String(Bitmap bmp)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                byte[] arr = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(arr, 0, (int)ms.Length);
                ms.Close();
                return Convert.ToBase64String(arr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据模板生成拼图
        /// </summary>
        /// <param name="baseImage"></param>
        /// <param name="templateImage"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private Bitmap CutByTemplate(Bitmap baseImage, Bitmap templateImage, int x, int y)
        {
            Bitmap newImage = new Bitmap(templateImage.Width, baseImage.Height, PixelFormat.Format32bppRgb);
            newImage.MakeTransparent();

            int xLength = templateImage.Width;
            int yLength = templateImage.Height;
            // 模板图像宽度
            for (int i = 0; i < xLength; i++)
            {
                // 模板图片高度
                for (int j = 0; j < yLength; j++)
                {
                    // 如果模板图像当前像素点不是透明色 copy源文件信息到目标图片中
                    int rgb = templateImage.GetPixel(i, j).ToArgb();
                    if (rgb < 0)
                    {
                        Color oriImageColor = baseImage.GetPixel(x + i, y + j);

                        newImage.SetPixel(i, y + j, oriImageColor);

                        //抠图区域半透明
                        baseImage.SetPixel(x + i, y + j, Color.FromArgb(120, oriImageColor.R, oriImageColor.G, oriImageColor.B));
                    }

                    //防止数组越界判断
                    if (i == (xLength - 1) || j == (yLength - 1))
                    {
                        continue;
                    }

                    int rightRgb = templateImage.GetPixel(i + 1, j).ToArgb();
                    int downRgb = templateImage.GetPixel(i, j + 1).ToArgb();
                    //描边处理，,取带像素和无像素的界点，判断该点是不是临界轮廓点,如果是设置该坐标像素是白色
                    if ((rgb >= 0 && rightRgb < 0) || (rgb < 0 && rightRgb >= 0) || (rgb >= 0 && downRgb < 0) || (rgb < 0 && downRgb >= 0))
                    {
                        newImage.SetPixel(i, y + j, Color.White);
                        baseImage.SetPixel(x + i, y + j, Color.White);
                    }
                }
            }
            return newImage;
        }

        /// <summary>
        /// 根据模板生成干扰图
        /// </summary>
        /// <param name="baseImage"></param>
        /// <param name="templateImage"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void InterferenceByTemplate(Bitmap baseImage, Bitmap templateImage, int x, int y)
        {
            int xLength = templateImage.Width;
            int yLength = templateImage.Height;
            // 模板图像宽度
            for (int i = 0; i < xLength; i++)
            {
                // 模板图片高度
                for (int j = 0; j < yLength; j++)
                {
                    // 如果模板图像当前像素点不是透明色 copy源文件信息到目标图片中
                    int rgb = templateImage.GetPixel(i, j).ToArgb();
                    if (rgb < 0)
                    {
                        Color oriImageColor = baseImage.GetPixel(x + i, y + j);

                        //抠图区域半透明
                        baseImage.SetPixel(x + i, y + j, Color.FromArgb(120, oriImageColor.R, oriImageColor.G, oriImageColor.B));
                    }

                    //防止数组越界判断
                    if (i == (xLength - 1) || j == (yLength - 1))
                    {
                        continue;
                    }

                    int rightRgb = templateImage.GetPixel(i + 1, j).ToArgb();
                    int downRgb = templateImage.GetPixel(i, j + 1).ToArgb();
                    //描边处理，,取带像素和无像素的界点，判断该点是不是临界轮廓点,如果是设置该坐标像素是白色
                    if ((rgb >= 0 && rightRgb < 0) || (rgb < 0 && rightRgb >= 0) || (rgb >= 0 && downRgb < 0) || (rgb < 0 && downRgb >= 0))
                    {
                        baseImage.SetPixel(x + i, y + j, Color.White);
                    }
                }
            }
        }
        

        /// <summary>
        /// 更改图片尺寸
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private Bitmap ResizeImage(Bitmap bmp, int width, int height)
        {
            try
            {
                Bitmap b = new Bitmap(width, height);
                Graphics g = Graphics.FromImage(b);
                // 图画质量
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, new Rectangle(0, 0, width, height), new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
                g.Dispose();

                return b;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 随机范围内数字
        /// </summary>
        /// <param name="startNum"></param>
        /// <param name="endNum"></param>
        /// <returns></returns>
		public int GetRandomInt(int startNum, int endNum)
        {
            return (endNum > startNum ? new Random().Next(endNum - startNum) : 0) + startNum;
        }

        /// <summary>
        /// 随机生成拼图坐标
        /// </summary>
        /// <param name="originalWidth"></param>
        /// <param name="originalHeight"></param>
        /// <param name="templateWidth"></param>
        /// <param name="templateHeight"></param>
        /// <returns></returns>
        private PointModel GeneratePoint(int originalWidth, int originalHeight, int templateWidth, int templateHeight)
        {
            Random random = new Random();
            int widthDifference = originalWidth - templateWidth;
            int heightDifference = originalHeight - templateHeight;
            int x;
            if (widthDifference <= 0)
            {
                x = 5;
            }
            else
            {
                x = random.Next(originalWidth - templateWidth - 100) + 100;
            }

            int y;
            if (heightDifference <= 0)
            {
                y = 5;
            }
            else
            {
                y = random.Next(originalHeight - templateHeight - 5) + 5;
            }

            return new PointModel(x, y);
        }

        /// <summary>
        /// 随机生成干扰图坐标
        /// </summary>
        /// <param name="originalWidth"></param>
        /// <param name="originalHeight"></param>
        /// <param name="templateWidth"></param>
        /// <param name="templateHeight"></param>
        /// <param name="blockX"></param>
        /// <param name="blockY"></param>
        /// <returns></returns>
        private PointModel GenerateInterferencePoint(int originalWidth, int originalHeight, int templateWidth, int templateHeight, int blockX, int blockY)
        {
            int x;
            if (originalWidth - blockX - 5 > templateWidth * 2)
            {
                //在原扣图右边插入干扰图
                x = GetRandomInt(blockX + templateWidth + 5, originalWidth - templateWidth);
            }
            else
            {
                //在原扣图左边插入干扰图
                x = GetRandomInt(100, blockX - templateWidth - 5);
            }

            int y;
            if (originalHeight - blockY - 5 > templateHeight * 2)
            {
                //在原扣图下边插入干扰图
                y = GetRandomInt(blockY + templateHeight + 5, originalHeight - templateHeight);
            }
            else
            {
                //在原扣图上边插入干扰图
                y = GetRandomInt(5, blockY - templateHeight - 5);
            }

            return new PointModel(x, y);
        }

        /// <summary>
        /// 获得验证数据
        /// </summary>
        /// <returns>JObject</returns>
        public async Task<CaptchaOutput> GetAsync()
        {
            //获取网络图片
            //var client = new HttpClient();
            //var stream = await client.GetStreamAsync("https://picsum.photos/310/155");
            //client.Dispose();

            //更改图片尺寸
            //Bitmap oriImage = new Bitmap(stream);
            //Bitmap baseImage = ResizeImage(oriImage, 310, 155);
            //oriImage.Dispose();

            //Bitmap baseImage = new Bitmap(stream);

            var oriImage = Image.FromFile(Directory.GetCurrentDirectory() + $@"\wwwroot\captcha\jigsaw\{new Random().Next(1, 4)}.jpg");
            Bitmap baseImage = new Bitmap(oriImage);
            oriImage.Dispose();

            var oriTemplate = Image.FromFile(Directory.GetCurrentDirectory() + $@"\wwwroot\captcha\jigsaw\templates\{new Random().Next(1, 7)}.png");
            Bitmap templateImage = new Bitmap(oriTemplate);
            oriTemplate.Dispose();

            int baseWidth = baseImage.Width;
            int baseHeight = baseImage.Height;
            int templateWidth = templateImage.Width;
            int templateHeight = templateImage.Height;

            //随机生成拼图坐标
            PointModel point = GeneratePoint(baseWidth, baseHeight, templateWidth, templateHeight);
            int x = point.X;
            int y = point.Y;

            //生成拼图
            string blockImageBase64 = "data:image/png;base64," + ImgToBase64String(CutByTemplate(baseImage, templateImage, x, y));

            //生成干扰图
            PointModel interferencePoint = GenerateInterferencePoint(baseWidth, baseHeight, templateWidth, templateHeight, x, y);
            InterferenceByTemplate(baseImage, templateImage, interferencePoint.X, interferencePoint.Y);

            string baseImageBase64 = "data:image/png;base64," + ImgToBase64String(baseImage);
            templateImage.Dispose();
            baseImage.Dispose();

            var token = Guid.NewGuid().ToString();
            CaptchaOutput captchaData = new CaptchaOutput
            {
                Token = token,
                Data = new SlideJigsawCaptchaModel()
                {
                    BlockImage = blockImageBase64,
                    BaseImage = baseImageBase64
                }
            };

            var key = string.Format(CacheKey.VerifyCodeKey, token);
            await _cache.SetAsync(key, point.X);

            return captchaData;
        }

        /// <summary>
        /// 检查验证数据
        /// </summary>
        public async Task<bool> CheckAsync(SlideJigsawCaptchaInput input)
        {
            var key = string.Format(CacheKey.VerifyCodeKey, input.Token);
            if (await _cache.ExistsAsync(key))
            {
                try
                {
                    var point = JsonConvert.DeserializeObject<PointModel>(input.Point);
                    var x = await _cache.GetAsync<int>(key);
                    if (Math.Abs(x - point.X) < 5)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    await _cache.DelAsync(key);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
