using System;
using System.Windows;

namespace CrowdDetection
{
    /// <summary>
    /// 画像データ、動き検知情報の管理(マスター)
    /// </summary>
    internal partial class ImageDataManager
    {
        /// <summary>
        /// 検知感度
        /// </summary>
        public int Sensitivity { get; set; } = 0;

        /// <summary>
        /// ImgDataの配列サイズ
        /// </summary>
        private const int IMAGE_DATA_NUM = 2;
        /// <summary>
        /// ImgDataの現在のインデックス
        /// </summary>
        private int _currentIndex = 0;
        /// <summary>
        /// 画像データ、動き検知情報の管理情報
        /// </summary>
        /// <remarks>検知の為に画像2枚分必要</remarks>
        private ImageData[] _imageDatas = new ImageData[IMAGE_DATA_NUM] { new ImageData(), new ImageData() };

        /// <summary>
        /// 検知領域差分用バッファ
        /// </summary>
        public int[] CabbBuffer { get; private set; } = null;

        /// <summary>
        /// 領域内での感度値の最大(感度自動設定用)
        /// </summary>
        public int DetectMaxSensitivity { get; private set; } = -1;

        /// <summary>
        /// 受信映像サイズ
        /// </summary>
        public Size RecvVideoImageSize { get; set; } = new Size(0, 0);

        /// <summary>
        /// 受信映像サイズのチェックを行ったか？
        /// </summary>
        private bool _isRecvVideoImageSizeChecked = false;

        /// <summary>
        /// ImgDataの次のインデックスを取得する
        /// </summary>
        /// <param name="currentIndex">現在のインデックス</param>
        /// <returns>int:次のインデックス</returns>
        private int GetNextIndex(int currentIndex) => currentIndex > 0 ? 0 : 1;

        /// <summary>
        /// 検知領域差分用バッファのクリア
        /// </summary>
        private void ClearCabb()
        {
            if (CabbBuffer != null)
            {
                Array.Clear(CabbBuffer, 0, CabbBuffer.Length);
            }
        }

        /// <summary>
        /// 検知領域差分用バッファのメモリ確保、初期化
        /// </summary>
        /// <param name="size">画像サイズ</param>
        private void SetCabb(Size size)
        {
            ClearCabb();

            int cabbSize = (((int)size.Width + 7) / 8) * (((int)size.Height + 7) / 8);

            if (CabbBuffer == null || cabbSize != CabbBuffer.Length)
            {
                CabbBuffer = new int[cabbSize];
            }
        }

        /// <summary>
        /// JPEG画像データを取得または設定する
        /// </summary>
        public byte[] JpegImageData
        {
            get
            {
                return _imageDatas[_currentIndex].JpegImageData;
            }
            set
            {
                _currentIndex = GetNextIndex(_currentIndex);
                _imageDatas[_currentIndex].JpegImageData = value;

                if (!_isRecvVideoImageSizeChecked)
                {
                    Size recvVideoImageSize = new Size(0, 0);
                    _imageDatas[_currentIndex].GetImageSize(ref recvVideoImageSize);
                    SetCabb(recvVideoImageSize);
                    RecvVideoImageSize = recvVideoImageSize;
                    _isRecvVideoImageSizeChecked = true;
                }
            }
        }

        /// <summary>
        /// フレーム間差分計算処理
        /// </summary>
        /// <param name="detectBlockCount">検知ブロックカウント</param>
        /// <returns>true:成功、false:失敗</returns>
        public bool CalcInterframeDifference(ref int detectBlockCount)
        {
            int x1 = 0;
            int y1 = 0;
            int x2 = 0;
            int y2 = 0;

            if (Sensitivity <= 0)
            {
                return false;
            }

            ClearCabb();

            // 1枚目の画像の縦横ブロック数を取得
            if (!_imageDatas[_currentIndex].GetBlockNumber(ref x1, ref y1))
            {
                return false;
            }

            // 2枚目の画像の縦横ブロック数を取得
            if (!_imageDatas[GetNextIndex(_currentIndex)].GetBlockNumber(ref x2, ref y2))
            {
                return false;
            }

            // 取得したブロック数をチェック
            if (x1 == 0 || x1 != x2 || x2 == 0 || y1 == 0 || y1 != y2 || y2 == 0 || CabbBuffer == null)
            {
                return false;
            }

            ushort q1 = _imageDatas[_currentIndex].GetQFactor();
            ushort q2 = _imageDatas[GetNextIndex(_currentIndex)].GetQFactor();
            if (q1 != q2)
            {
                return false;
            }

            // imageのサイズ等々の整合性チェック
            short[] psCoeffYCur = _imageDatas[_currentIndex].GetQDCT_COEFF();
            short[] psCoeffYPre = _imageDatas[GetNextIndex(_currentIndex)].GetQDCT_COEFF();
            int coeffYCurPosition = 0;
            int coeffYPrePosition = 0;
            if (psCoeffYCur == null || psCoeffYPre == null)
            {
                return false;
            }

            // 量子化テーブル(QT)はQ値により次のように異なる．
            // Q ≧ 50の時，QT = MAX(QToriginal * (100 - Q)/50, 1)
            // Q <  50の時，QT = MIN(QToriginal * 50 / Q, 255) 
            // Q > 68のときだけ，DCT差分値*(100-Q)/32 により感度を鈍くするよう補正する．
            // (100 - 68)/32 = 1 によりQ による変化でQ=68前後で離散的にならない
            int nQAdjust = 0;
            int nShift = 0;
            if (q1 > 68)
            {
                if (q1 < 97)
                {
                    nShift = 5;
                    nQAdjust = 100 - q1;
                }
                else
                {
                    // Q > 96の時は，QTの値が0となる成分が増え1で置き換えることによる過補正となる．
                    // Q=97の時にQ=100の補正を適用すると，実験的に適正値になることがわかっている．
                    // 97：差分*15>>(5+2), 98：差分*14>>(5+2), 99：差分*13>>(5+2)とする
                    nShift = 7; //6
                    nQAdjust = 112 - q1;
                }
            }
            else
            {
                nShift = 0;
                nQAdjust = 1;
            }

            int cabbPosition = 0;
            DetectMaxSensitivity = -1;
            detectBlockCount = 0;

            // 垂直方向のブロック単位で処理する。
            for (int blkY = 0; blkY < y1; blkY++)
            {
                // 水平方向のブロック単位で処理する。
                for (int blkX = 0; blkX < x1; blkX++)
                {
                    // 変化検知領域内の場合
                    int diffAbsY = 0;

                    // 量子化後のDCT係数列は1ブロック中に64個含まれる。
                    for (int k = 0; k < 64; k++)
                    {
                        // 輝度成分の量子化後のDCT係数のフレーム間差分の絶対値を積算する。
                        int diffY = 0;
                        diffAbsY += ((diffY = psCoeffYCur[coeffYCurPosition++] - psCoeffYPre[coeffYPrePosition++]) >= 0 ? 1 : -1) * diffY;
                    }

                    diffAbsY = (diffAbsY * nQAdjust) >> nShift;
                    diffAbsY = (diffAbsY >= 255) ? 255 : diffAbsY;
                    CabbBuffer[cabbPosition++] = diffAbsY;

                    if (diffAbsY >= Sensitivity)
                    {
                        detectBlockCount += 1;
                    }

                    if (diffAbsY >= DetectMaxSensitivity)
                    {
                        DetectMaxSensitivity = diffAbsY;
                    }
                }
            }

            return true;
        }
    }
}
