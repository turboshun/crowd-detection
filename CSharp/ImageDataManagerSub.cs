using System.Windows;

namespace CrowdDetection
{
    /// <summary>
    /// 画像データ、動き検知情報の管理(マスター)
    /// </summary>
    internal partial class ImageDataManager
    {
        /// <summary>
        /// 画像データ、動き検知情報の管理
        /// </summary>
        private class ImageData
        {
            /// <summary>
            /// 量子化後のDCT係数列等の取得、管理用
            /// </summary>
            private QDCTCoefficient _qdctCoefficient = null;

            /// <summary>
            /// JPEGデータ
            /// </summary>
            private byte[] _jpegImageData = null;
            /// <summary>
            /// JPEGデータを取得または設定する
            /// </summary>
            public byte[] JpegImageData
            {
                get { return _jpegImageData; }
                set
                {
                    if (_qdctCoefficient == null)
                    {
                        _qdctCoefficient = new QDCTCoefficient();
                    }

                    _qdctCoefficient.SetJpegToDCTCoeff(value);
                    _jpegImageData = value;
                }
            }

            /// <summary>
            /// 映像サイズを取得する
            /// </summary>
            /// <param name="imageSize">イメージサイズ</param>
            public void GetImageSize(ref Size imageSize)
            {
                if (_qdctCoefficient != null)
                {
                    int w = 0, h = 0;
                    _qdctCoefficient.GetImageSize(ref w, ref h);
                    imageSize.Width = w;
                    imageSize.Height = h;
                }
            }

            /// <summary>
            /// 8x8ブロック数を取得
            /// </summary>
            /// <param name="blockNumberX">水平方向の8x8ブロック数</param>
            /// <param name="blockNumberY">垂直方向の8x8ブロック数</param>
            /// <returns>取得に成功したか？</returns>
            public bool GetBlockNumber(ref int blockNumberX, ref int blockNumberY)
            {
                if (_qdctCoefficient != null)
                {
                    _qdctCoefficient.GetBlockNumber(ref blockNumberX, ref blockNumberY);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Q Factorを取得
            /// </summary>
            /// <returns>ushort:Q Factor</returns>
            public ushort GetQFactor()
            {
                return (_qdctCoefficient != null ? _qdctCoefficient.GetQFactor() : (ushort)0);
            }

            /// <summary>
            /// 輝度(Y)成分の量子化後のDCT係数列を取得
            /// </summary>
            /// <returns>short[]:輝度(Y)成分の量子化後のDCT係数列</returns>
            public short[] GetQDCT_COEFF()
            {
                return _qdctCoefficient?.GethQDCT_COEFF();
            }
        }
    }
}
