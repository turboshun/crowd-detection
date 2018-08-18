using System;
using System.Collections.Generic;

namespace CrowdDetection
{
    /// <summary>
    /// 量子化後のDCT係数列等の取得、管理
    /// </summary>
    internal partial class QDCTCoefficient
    {
        /// <summary>
        /// JPEGスキャニングクラス
        /// </summary>
        private class ScanJpeg
        {
            /// <summary>
            /// 戻り値一覧
            /// </summary>
            private enum ReturnID
            {
                /// <summary> 正常終了 </summary>
                Success = 0,
                /// <summary> 不正な引数 </summary>
                ErrorInvalidParam,
                /// <summary> メモリ不足 </summary>
                ErrorShortOfMemory,
                /// <summary> 内部処理エラー </summary>
                ErrorInternal,
                /// <summary> JPEGデータ不足 </summary>
                JpegDataErrorShortOfData,
                /// <summary> JPEGマーカ不足 </summary>
                JpegDataErrorLackOfMarker,
                /// <summary> JPEGデータの先頭にSOIマーカがない </summary>
                JpegDataErrorNoSOIMarker,
                /// <summary> JPEGデータの最後にEOIマーカがない </summary>
                JpegDataErrorNoEOIMarker,
                /// <summary> 予期せぬJPEGマーカ </summary>
                JpegDataErrorUnexpectedMarker,
                /// <summary> 不正なJPEGマーカセグメント </summary>
                JpegDataErrorBadMmarkerSegment,
                /// <summary> JPEGデータにサポート外の構文 </summary>
                JpegDataErrorUnsupported,
                /// <summary> 予期せぬJPEGデータ </summary>
                JpegDataErrorUnexpectedData,
            }

            /// <summary>
            /// JPEGマーカコード
            /// </summary>
            private enum JpegMarkerCodes : byte
            {
                /// <summary> マーカーの第一バイトまたはフィルバイト </summary>
                MARKER_ID = 0xFF,
                /// <summary> Temporary (算術符号処理用) </summary>
                TEM = 0x01,
                /// <summary> Restart 0 </summary>
                RST0 = 0xD0,
                /// <summary> Restart 1 </summary>
                RST1 = 0xD1,
                /// <summary> Restart 2 </summary>
                RST2 = 0xD2,
                /// <summary> Restart 3 </summary>
                RST3 = 0xD3,
                /// <summary> Restart 4 </summary>
                RST4 = 0xD4,
                /// <summary> Restart 5 </summary>
                RST5 = 0xD5,
                /// <summary> Restart 6 </summary>
                RST6 = 0xD6,
                /// <summary> Restart 7 </summary>
                RST7 = 0xD7,
                /// <summary> Start Of Image </summary>
                SOI = 0xD8,
                /// <summary> End Of Image </summary>
                EOI = 0xD9,
                /// <summary> Start Of Frame: Baseline DCT (Huffman) </summary>
                SOF0 = 0xC0,
                /// <summary> Define Huffman Table </summary>
                DHT = 0xC4,
                /// <summary> Start Of Scan </summary>
                SOS = 0xDA,
                /// <summary> Define Quantization Table </summary>
                DQT = 0xDB,
                /// <summary> Restart Interval定義 </summary>
                DRI = 0xDD,
                /// <summary> Application Data </summary>
                APP0 = 0xE0,
            }

            /// <summary>
            /// ハフマンテーブル定義補助クラス
            /// </summary>
            private class DefineHuffmanTableSub
            {
                /// <summary> Tc:ハフマンテーブルクラス(0:DC成分 1:AC成分) </summary>
                public byte Tc { get; set; }
                /// <summary> Th:ハフマンテーブル番号(0-3) </summary>
                public byte Th { get; set; }
                /// <summary> 下記ハフマン符号数の合計(256以下の値) </summary>
                public ushort Total { get; set; } = 0;
                /// <summary> Li:長さi(bit)のハフマン符号数 </summary>
                public byte[] Li { get; set; } = new byte[16];
                /// <summary> Vij:各ハフマン符号に対応する値 </summary>
                public byte[] Vij { get; set; } = new byte[256];
            }

            /// <summary>
            /// ハフマン復号テーブルクラス
            /// </summary>
            private class HuffmanDecodeTable
            {
                /// <summary> 最大符号語長(ビット数 1-16) </summary>
                public byte BitLengthMax { get; set; }
                /// <summary> 符号語長/シンボル値の配列のインデックスの最大値 </summary>
                public byte IndexMax { get; set; }
                /// <summary> ハフマン復号用のLookUpTable </summary>
                public byte[] CodeToIndexLookUpTable { get; set; } = null;
                /// <summary> 符号語長 </summary>
                public byte[] BitLength { get; set; } = new byte[256];
                /// <summary> シンボル値 </summary>
                public byte[] Symbol { get; set; } = new byte[256];
            }

            /// <summary>
            /// JPEGデータ
            /// </summary>
            private byte[] _jpegData = null;

            /// <summary>
            /// JPEGデータ現在位置
            /// </summary>
            private long _jpegDataIndex = 0;

            /// <summary>
            /// JPEGデータ未読サイズ(バイト数)
            /// </summary>
            private long _remainSize = 0;

            /// <summary>
            /// JPEGデータ未読ビット数(_jpegData値のLSB側が未読)
            /// </summary>
            private int _remainBitLength = 0;

            /// <summary>
            /// 量子化後のDCT係数列
            /// </summary>
            private QDCTCoefficientParameter _qdctCoeffParam = null;

            /// <summary>
            /// 水平サンプリングレートの輝度/色差比率(1-2)
            /// </summary>
            private int _hiYCR = 0;

            /// <summary>
            /// 垂直サンプリングレートの輝度/色差比率(1-2)
            /// </summary>
            private int _viYCR = 0;

            /// <summary>
            /// 直流係数用ハフマン復号テーブル(DC HDT)の配列
            /// </summary>
            private HuffmanDecodeTable[] _dcHDT = new HuffmanDecodeTable[2];

            /// <summary>
            /// 交流係数用ハフマン復号テーブル(AC HDT)の配列
            /// </summary>
            private HuffmanDecodeTable[] _acHDT = new HuffmanDecodeTable[2];

            /// <summary>
            /// 画像成分数
            /// </summary>
            private int _components = 0;

            /// <summary>
            /// 画像成分(Y,Cb,Cr)識別子の配列
            /// </summary>
            private int[] _componentID = new int[3];

            /// <summary>
            /// 水平/垂直サンプリングレートの配列
            /// </summary>
            private int[] _hvSamplRate = new int[3];

            /// <summary>
            /// 画像成分ごとのDC HDTセレクタの配列
            /// </summary>
            private int[] _dcHDTSel = new int[3];

            /// <summary>
            /// 画像成分ごとのAC HDTセレクタの配列
            /// </summary>
            private int[] _acHDTSel = new int[3];

            /// <summary>
            /// コンストラクタ
            /// </summary>
            public ScanJpeg()
            {
                for (int i = 0; i < _dcHDT.Length; i++)
                {
                    _dcHDT[i] = new HuffmanDecodeTable();
                    _acHDT[i] = new HuffmanDecodeTable();
                }
            }

            /// <summary>
            /// JPEGデータから量子化後のDCT係数列を取得する。
            /// </summary>
            /// <param name="jpegData">JPEGデータ</param>
            /// <param name="qdctCoeff">量子化後のDCT係数列構造体</param>
            /// <param name="qFactor">QFactor</param>
            /// <returns>処理結果</returns>
            public bool GetQuantizedDCTCoeff(byte[] jpegData, out QDCTCoefficientParameter qdctCoeff, out ushort qFactor)
            {
                qdctCoeff = new QDCTCoefficientParameter();
                qFactor = 0;

                if (jpegData == null || jpegData.Length < 2)
                {
                    //return ReturnID.ErrorInvalidParam;
                    return false;
                }

                _jpegData = (byte[])jpegData.Clone();
                _jpegDataIndex = 0;
                _remainSize = jpegData.Length;
                _remainBitLength = 8;
                _qdctCoeffParam = new QDCTCoefficientParameter();
                _hiYCR = 0;
                _viYCR = 0;

                // JPEGデータの先頭にSOIマーカがなければならない。
                if ((byte)JpegMarkerCodes.MARKER_ID != _jpegData[0] || (byte)JpegMarkerCodes.SOI != _jpegData[1])
                {
                    // JPEGデータの先頭にSOIマーカがない
                    //return ReturnID.JpegDataErrorNoSOIMarker;
                    return false;
                }

                _jpegDataIndex += 2;
                _remainSize -= 2;

                // スキャンデータ直前までの全マーカを読み出す。正常終了時、SOSマーカセグメントまで既読。
                ReturnID returnID = ReadMarkersBeforeScanData(out bool readDHT, out bool readSOF0, out ushort restartInterval, out qFactor);

                if (ReturnID.Success == returnID)
                {
                    if (!readDHT || !readSOF0)
                        returnID = ReturnID.JpegDataErrorLackOfMarker;
                    else if (restartInterval != 0)
                        returnID = ReturnID.JpegDataErrorUnsupported;
                    else
                        returnID = ReadScanData();
                }

                if (ReturnID.Success != returnID)
                {
                    _qdctCoeffParam.YComponents = null;
                }

                _qdctCoeffParam.Copy(ref qdctCoeff);

                return (ReturnID.Success == returnID);
            }

            /// <summary>
            /// スキャンデータ直前までの全マーカを読み出す。正常終了時、SOSマーカセグメントまで既読。
            /// </summary>
            /// <param name="readDHT">DHTマーカ読み出し済みフラグ</param>
            /// <param name="readSOF0">SOF0マーカ読み出し済みフラグ</param>
            /// <param name="restartInterval">Restart Interval</param>
            /// <param name="qFactor">QFactor</param>
            /// <returns>処理結果</returns>
            private ReturnID ReadMarkersBeforeScanData(out bool readDHT, out bool readSOF0, out ushort restartInterval, out ushort qFactor)
            {
                readDHT = false;
                readSOF0 = false;
                restartInterval = 0;
                qFactor = 0;

                // JPEGマーカコードが残っている間は処理を繰り返す。
                while (_remainSize >= 2 && (byte)JpegMarkerCodes.MARKER_ID == _jpegData[_jpegDataIndex])
                {
                    // (マーカに先行するフィルバイトおよび)マーカの第一バイトを読み飛ばす。
                    while ((byte)JpegMarkerCodes.MARKER_ID == _jpegData[_jpegDataIndex])
                    {
                        _jpegDataIndex++;
                        _remainSize--;
                    }

                    if (--_remainSize <= 0)
                        return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                    // マーカの第二バイトの値に応じて処理を分ける。
                    ReturnID returnID;
                    ushort length;
                    switch ((JpegMarkerCodes)_jpegData[_jpegDataIndex++])
                    {
                        case JpegMarkerCodes.DHT:          // ハフマンテーブル定義
                            {
                                // DHTマーカセグメントを読み出し、ハフマン復号テーブルを作成する。
                                if (ReturnID.Success != (returnID = ReadDHTMarkerSeg()))
                                    return returnID;

                                readDHT = true;
                            }
                            break;
                        case JpegMarkerCodes.SOF0:         // フレームヘッダ: Baseline DCT (Huffman)
                            {
                                // SOF0マーカセグメントを読み出す。
                                if (ReturnID.Success != (returnID = ReadSOF0MarkerSeg()))
                                    return returnID;

                                readSOF0 = true;
                            }
                            break;
                        case JpegMarkerCodes.SOS:          // スキャンヘッダ
                                                           // SOSマーカセグメントを読み出す。
                            return ReadSOSMarkerSeg();
                        case JpegMarkerCodes.DRI:          // Restart Interval定義
                            {
                                length = (ushort)((_jpegData[_jpegDataIndex] << 8) + _jpegData[_jpegDataIndex + 1]);
                                if (length != 4)
                                    return ReturnID.JpegDataErrorBadMmarkerSegment;   // 不正なJPEGマーカセグメント

                                restartInterval = (ushort)((_jpegData[_jpegDataIndex + 2] << 8) + _jpegData[_jpegDataIndex + 3]);
                                _jpegDataIndex += length;
                                _remainSize -= length;
                            }
                            break;
                        case JpegMarkerCodes.SOI:          // SOI
                        case JpegMarkerCodes.EOI:          // EOI
                        case JpegMarkerCodes.TEM:          // TEM
                            return ReturnID.JpegDataErrorUnexpectedMarker;        // 予期せぬJPEGマーカ
                        default:
                            {
                                // リスタートマーカが現れた場合
                                if ((_jpegData[_jpegDataIndex] >= (byte)JpegMarkerCodes.RST0) && (_jpegData[_jpegDataIndex] <= (byte)JpegMarkerCodes.RST7))
                                    return ReturnID.JpegDataErrorUnexpectedMarker;        // 予期せぬJPEGマーカ

                                // その他のマーカは読み飛ばす。
                                length = (ushort)((_jpegData[_jpegDataIndex] << 8) + _jpegData[_jpegDataIndex + 1]);
                                _jpegDataIndex += length;
                                _remainSize -= length;
                            }
                            break;
                    }
                }

                return ReturnID.JpegDataErrorLackOfMarker;       // JPEGマーカ不足
            }

            /// <summary>
            /// DHTマーカセグメントを読み出し、ハフマン復号テーブルを作成する。
            /// </summary>
            /// <returns>処理結果</returns>
            private ReturnID ReadDHTMarkerSeg()
            {
                // DHTマーカセグメント長
                int length = (_jpegData[_jpegDataIndex] << 8) + _jpegData[_jpegDataIndex + 1];
                if (length > _remainSize)
                    return ReturnID.JpegDataErrorShortOfData;        // JPEGデータ不足
                else if (length <= 19)
                    return ReturnID.JpegDataErrorBadMmarkerSegment;   // 不正なJPEGマーカセグメント

                _jpegDataIndex += 2;
                _remainSize -= 2;
                length -= 2;

                // ハフマンテーブル定義の個数分ループする。
                while (length > 0)
                {
                    // ハフマンテーブル定義補助構造体に値を代入する。
                    DefineHuffmanTableSub dhts = new DefineHuffmanTableSub()
                    {
                        Tc = (byte)((_jpegData[_jpegDataIndex] >> 4) & 0x0F),
                        Th = (byte)(_jpegData[_jpegDataIndex++] & 0x0F),
                    };

                    Array.Copy(_jpegData, _jpegDataIndex, dhts.Li, 0, 16);
                    _jpegDataIndex += 16;

                    for (int i = 0; i < 16; i++)
                        dhts.Total += dhts.Li[i];

                    if (dhts.Total > 256)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    Array.Copy(_jpegData, _jpegDataIndex, dhts.Vij, 0, dhts.Total);
                    _jpegDataIndex += dhts.Total;
                    _remainSize -= 17 + dhts.Total;
                    length -= 17 + dhts.Total;

                    if (_remainSize < 0)
                        return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                    // ハフマン復号テーブルを作成する。
                    ReturnID returnID = MakeHuffDecTbl(dhts);
                    if (ReturnID.Success != returnID)
                        return returnID;
                }

                return ReturnID.Success;
            }

            /// <summary>
            /// ハフマン復号テーブルを作成する。
            /// </summary>
            /// <param name="dhts">ハフマンテーブル定義補助</param>
            /// <returns>処理結果</returns>
            private ReturnID MakeHuffDecTbl(DefineHuffmanTableSub dhts)
            {
                if (dhts == null || dhts.Tc > 1 || dhts.Th > 1 || dhts.Total == 0 || dhts.Total > 256)
                {
                    return ReturnID.ErrorInvalidParam;      // 不正な引数
                }

                // 処理対象となるハフマン復号テーブルを取得する。
                HuffmanDecodeTable hdt = dhts.Tc == 0 ? _dcHDT[dhts.Th] : _acHDT[dhts.Th];

                // ハフマン復号テーブルをクリアする。
                hdt.BitLengthMax = 0;
                hdt.IndexMax = 0;
                if (hdt.CodeToIndexLookUpTable != null)
                {
                    hdt.CodeToIndexLookUpTable = null;
                }
                Array.Clear(hdt.BitLength, 0, hdt.BitLength.Length);
                Array.Clear(hdt.Symbol, 0, hdt.Symbol.Length);

                // 符号語格納メモリ(符号語テーブル)を確保する。
                ushort[] codeTable = new ushort[dhts.Total];

                // 符号語長の配列と符号語テーブルに値を代入する。
                byte bitLengthMax = 0;
                ushort code = 0;
                int bitLengthIndex = 0;
                int codeTableIndex = 0;
                for (byte bitLength = 1; bitLength <= 16; bitLength++)
                {
                    byte numOfCodes = dhts.Li[bitLength - 1];
                    if (numOfCodes != 0)
                    {
                        bitLengthMax = bitLength;

                        for (byte count = 0; count < numOfCodes; count++)
                        {
                            hdt.BitLength[bitLengthIndex++] = bitLength;
                            codeTable[codeTableIndex++] = code++;
                        }
                    }
                    code <<= 1;
                }

                if (bitLengthMax == 0)
                {
                    return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ
                }

                // ハフマン復号用のLookUpTableのメモリを確保し、-1で初期化する。
                hdt.CodeToIndexLookUpTable = new byte[1 << bitLengthMax];
                for (int i = 0; i < hdt.CodeToIndexLookUpTable.Length; i++)
                {
                    hdt.CodeToIndexLookUpTable[i] = 0xFF;
                }
                // ハフマン復号テーブルに適切な値を代入する。
                hdt.BitLengthMax = bitLengthMax;
                hdt.IndexMax = (byte)(dhts.Total - 1);
                Array.Copy(dhts.Vij, 0, hdt.Symbol, 0, dhts.Total);

                // ハフマン復号用のLookUpTableに値を代入する。
                bitLengthIndex = 0;
                codeTableIndex = 0;
                for (ushort wIdx = 0; wIdx < dhts.Total; wIdx++)
                {
                    // 最大符号語長と各符号語長との差(nLenDiff)を求める。
                    int nLenDiff = bitLengthMax - hdt.BitLength[bitLengthIndex++];
                    if (nLenDiff != 0)
                    {
                        // 符号語をMSB側に寄せた値をLUTの最初のインデックスとし、そこから必要な個数(注)
                        // だけ符号語長およびシンボル値の配列のインデックスを代入する。
                        // (注) 符号語をMSB側に寄せたためにLSB側に空いたビットを埋めるのに必要な個数。
                        ushort wLUTIdx = (ushort)(codeTable[codeTableIndex++] << nLenDiff);
                        for (int i = 0; i < 1 << nLenDiff; i++)
                        {
                            hdt.CodeToIndexLookUpTable[wLUTIdx + i] = (byte)wIdx;
                        }
                    }
                    else
                        hdt.CodeToIndexLookUpTable[codeTable[codeTableIndex++]] = (byte)wIdx;
                }

                return ReturnID.Success;
            }

            /// <summary>
            /// SOF0マーカセグメントを読み出す。
            /// </summary>
            /// <returns>処理結果</returns>
            private ReturnID ReadSOF0MarkerSeg()
            {
                // フレームヘッダ長 (SOF0マーカセグメント長)
                int length = (_jpegData[_jpegDataIndex] << 8) + _jpegData[_jpegDataIndex + 1];
                if (length > _remainSize)
                    return ReturnID.JpegDataErrorShortOfData;        // JPEGデータ不足
                else if (length < 11)
                    return ReturnID.JpegDataErrorBadMmarkerSegment;   // 不正なJPEGマーカセグメント

                // 画像サイズ (ライン数、ライン当たりの標本数)
                _qdctCoeffParam.Height = (_jpegData[_jpegDataIndex + 3] << 8) + _jpegData[_jpegDataIndex + 4];
                _qdctCoeffParam.Width = (_jpegData[_jpegDataIndex + 5] << 8) + _jpegData[_jpegDataIndex + 6];
                if (_qdctCoeffParam.Height <= 0)
                    return ReturnID.JpegDataErrorUnsupported;          // JPEGデータにサポート外の構文
                else if (_qdctCoeffParam.Width <= 0 || _qdctCoeffParam.Width > short.MaxValue || _qdctCoeffParam.Height > short.MaxValue)
                    return ReturnID.JpegDataErrorUnexpectedData;      // 予期せぬJPEGデータ

                // フレーム内の画像成分数 == 3(YCbCr) か == 1(Y) のはず。
                if (3 != _jpegData[_jpegDataIndex + 7] && 1 != _jpegData[_jpegDataIndex + 7])
                    return ReturnID.JpegDataErrorUnexpectedData;      // 予期せぬJPEGデータ

                _components = _jpegData[_jpegDataIndex + 7];

                _jpegDataIndex += 8;
                _remainSize -= 8;

                if (_remainSize < 9)
                    return ReturnID.JpegDataErrorShortOfData;        // JPEGデータ不足

                for (int i = 0; i < _components; i++, _jpegDataIndex += 3, _remainSize -= 3)
                {
                    _componentID[i] = _jpegData[_jpegDataIndex];
                    _hvSamplRate[i] = _jpegData[_jpegDataIndex + 1];
                    // 量子化表セレクタ(_jpegData[ 2 ])は使用しないので無視する。
                }
                if (_components == 3 && (_componentID[0] == _componentID[1] || _componentID[0] == _componentID[2] || _componentID[1] == _componentID[2]))
                    return ReturnID.JpegDataErrorUnexpectedData;      // 予期せぬJPEGデータ

                return ReturnID.Success;
            }

            /// <summary>
            /// SOSマーカセグメントを読み出す。
            /// </summary>
            /// <returns>処理結果</returns>
            private ReturnID ReadSOSMarkerSeg()
            {
                // スキャンヘッダ長 (SOSマーカセグメント長)
                int length = (_jpegData[_jpegDataIndex] << 8) + _jpegData[_jpegDataIndex + 1];
                if (length > _remainSize)
                    return ReturnID.JpegDataErrorShortOfData;        // JPEGデータ不足
                else if (length < 8)
                    return ReturnID.JpegDataErrorBadMmarkerSegment;   // 不正なJPEGマーカセグメント

                // スキャン内の画像成分数 == 3(YCbCr) か == 1(Y) のはず。
                if (3 != _jpegData[_jpegDataIndex + 2] && 1 != _jpegData[_jpegDataIndex + 2])
                    return ReturnID.JpegDataErrorUnexpectedData;      // 予期せぬJPEGデータ
                _components = _jpegData[_jpegDataIndex + 2];

                _jpegDataIndex += 3;
                _remainSize -= 3;

                if (_remainSize < 6)
                    return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                for (int j = 0; j < _components; j++, _jpegDataIndex += 2, _remainSize -= 2)
                {
                    // スキャンヘッダ中の画像成分識別子は、フレームヘッダ中の画像成分識別子の
                    // いずれか一つと一致しなければならない。
                    int componentID = (int)_jpegData[_jpegDataIndex];
                    int idx;
                    for (idx = 0; idx < _components; idx++)
                    {
                        if (componentID == _componentID[idx])
                            break;
                    }
                    if (idx >= _components)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    // スキャンデータは Y,Cb,Cr 成分の順で現れ、これに対応するサンプリングレートは
                    // Y:Cb:Cr = 4:1:1, 2:1:1, 1:1:1 のいずれかであると仮定している。
                    if (0 == j)
                    {
                        switch (_hvSamplRate[idx])
                        {
                            case 0x11:
                            case 0x12:
                            case 0x21:
                            case 0x22:
                                _hiYCR = _hvSamplRate[idx] >> 4;
                                _viYCR = _hvSamplRate[idx] & 0x0F;
                                break;
                            default:
                                return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ
                        }

                        _qdctCoeffParam.BlockNumX = (int)(1 == _hiYCR ? CalcUint8Num((uint)_qdctCoeffParam.Width) : (CalcUint16Num((uint)_qdctCoeffParam.Width) << 1));
                        _qdctCoeffParam.BlockNumY = (int)(1 == _viYCR ? CalcUint8Num((uint)_qdctCoeffParam.Height) : (CalcUint16Num((uint)_qdctCoeffParam.Height) << 1));
                    }
                    else if (0x11 != _hvSamplRate[idx])
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    // DC/ACエントロピー符号化表セレクタは 0 または 1 のはず。
                    byte tdTa = _jpegData[_jpegDataIndex + 1];
                    if ((tdTa & ~0x11) != 0)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    _dcHDTSel[j] = (tdTa >> 4) & 0x0F;
                    _acHDTSel[j] = tdTa & 0x0F;
                }

                if (_remainSize < 3)
                    return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                _jpegDataIndex += 3;
                _remainSize -= 3;

                return ReturnID.Success;
            }

            /// <summary>
            /// スキャンデータを読み出す。
            /// </summary>
            /// <returns>処理結果</returns>
            private ReturnID ReadScanData()
            {
                // スキャンデータを読み出す(実行関数)。
                ReturnID returnID = DoReadScanData(out short[] qDctY);

                _qdctCoeffParam.YComponents = (ReturnID.Success == returnID ? (short[])qDctY.Clone() : null);

                return returnID;
            }

            /// <summary>
            /// スキャンデータを読み出す(実行関数)。
            /// </summary>
            /// <param name="qDctY">輝度成分の量子化後のDCT係数列格納メモリ</param>
            /// <returns>処理結果</returns>
            private ReturnID DoReadScanData(out short[] qDctY)
            {
                qDctY = null;

                // 8x8ブロック数およびサンプリングレートの輝度/色差比率が正しく設定されていなければならない。
                if (_qdctCoeffParam.BlockNumX <= 0 || _qdctCoeffParam.BlockNumX > short.MaxValue ||
                    _qdctCoeffParam.BlockNumY <= 0 || _qdctCoeffParam.BlockNumY > short.MaxValue ||
                    _hiYCR < 1 || _hiYCR > 2 ||
                    _viYCR < 1 || _viYCR > 2)
                {
                    return ReturnID.ErrorInternal;   // 内部処理エラー
                }

                qDctY = new short[_qdctCoeffParam.BlockNumX * _qdctCoeffParam.BlockNumY * 64];

                short[] preds = { 0, 0, 0 };
                int sizeTemp = _qdctCoeffParam.BlockNumX << 6;
                List<int> qDctYIndexList = new List<int> { 0, (1 == _hiYCR ? sizeTemp : 64), sizeTemp, sizeTemp + 64 };

                // 垂直方向のMCU単位で処理する。
                for (int nMcuY = 0; nMcuY < _qdctCoeffParam.BlockNumY / _viYCR; nMcuY++)
                {
                    // 水平方向のMCU単位で処理する。
                    for (int nMcuX = 0; nMcuX < _qdctCoeffParam.BlockNumX / _hiYCR; nMcuX++)
                    {
                        for (int component = 0; component < _components; component++)
                        {
                            // ハフマン復号テーブルセレクタはいずれも 0 または 1 でなければならない。
                            int nDcSel = _dcHDTSel[component];
                            int nAcSel = _acHDTSel[component];
                            if ((nDcSel & ~1) != 0 || (nAcSel & ~1) != 0)
                            {
                                return ReturnID.ErrorInternal;   // 内部処理エラー
                            }

                            if (0 == component)    // 輝度成分
                            {
                                // 1 MCUには輝度ブロックが複数含まれる。
                                for (int i = 0; i < _hiYCR * _viYCR; i++)
                                {
                                    // ブロックデータを読み出す。
                                    ReturnID returnID = ReadBlockData(ref qDctY, qDctYIndexList[i], ref preds[0], _dcHDT[nDcSel], _acHDT[nAcSel]);
                                    if (ReturnID.Success != returnID)
                                        return returnID;

                                    qDctYIndexList[i] += _hiYCR << 6;
                                }
                            }
                            else                    // 色差成分
                            {
                                // ブロックデータを読み出す。
                                short[] qDctYTemp = null;
                                ReturnID returnID = ReadBlockData(ref qDctYTemp, 0, ref preds[component], _dcHDT[nDcSel], _acHDT[nAcSel]);
                                if (ReturnID.Success != returnID)
                                    return returnID;
                            }
                        }
                    }

                    if (2 == _viYCR)
                    {
                        for (int i = 0; i < _hiYCR * _viYCR; i++)
                            qDctYIndexList[i] += sizeTemp;
                    }
                }

                if ((_remainBitLength & 7) != 0)
                {
                    // 後続のビットを読み出す。これらはすべてビット1(fill bits)のはず。
                    // 注意：SjsReadBits 関数呼び出し中に _remainBitLength 値が変わってしまうので、
                    //		下記の様に nFillBitLen に代入しておく必要がある。
                    int nFillBitLen = _remainBitLength & 7;
                    ushort wFillBits = ReadBits(nFillBitLen);
                }

                if (_remainSize < 2)
                    return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                // 最後に、EOIマーカがあることを確認する。
                while ((_remainSize >= 2) && ((byte)JpegMarkerCodes.MARKER_ID == _jpegData[_jpegDataIndex]))
                {
                    // (マーカに先行するフィルバイトおよび)マーカの第一バイトを読み飛ばす。
                    while ((byte)JpegMarkerCodes.MARKER_ID == _jpegData[_jpegDataIndex])
                    {
                        _jpegDataIndex++;
                        _remainSize--;
                    }

                    if (--_remainSize < 0)
                        return ReturnID.JpegDataErrorShortOfData;    // JPEGデータ不足

                    // EOIマーカがあれば、正常終了。
                    if ((byte)JpegMarkerCodes.EOI == _jpegData[_jpegDataIndex++])
                        return ReturnID.Success;
                }

                return ReturnID.JpegDataErrorNoEOIMarker;            // JPEGデータの最後にEOIマーカがない
            }

            private readonly short[] _positive = { 0x0000, 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400 };
            private readonly short[] _makeNegative = { 0, -1, -3, -7, -15, -31, -63, -127, -255, -511, -1023, -2047 };

            /// <summary>
            /// ブロックデータを読み出す。
            /// </summary>
            /// <param name="qDct">量子化後のDCT係数列格納メモリ</param>
            /// <param name="qDctIndex">qDctのインデックス</param>
            /// <param name="pred">直流係数予測値</param>
            /// <param name="dcHDT">直流係数用ハフマン復号テーブル(DC HDT)</param>
            /// <param name="acHDT">交流係数用ハフマン復号テーブル(AC HDT)</param>
            /// <returns>処理結果</returns>
            private ReturnID ReadBlockData(ref short[] qDct, int qDctIndex, ref short pred, HuffmanDecodeTable dcHDT, HuffmanDecodeTable acHDT)
            {
                // 直流係数を復号する。
                {
                    // 直流係数と予測値の差分のカテゴリ(=後続のビット数)を復号する。
                    byte index = dcHDT.CodeToIndexLookUpTable[PeepBits(dcHDT.BitLengthMax)];
                    if (index > dcHDT.IndexMax)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    SkipBits(dcHDT.BitLength[index]);

                    byte dcDiffBitLength = dcHDT.Symbol[index];
                    if (dcDiffBitLength > 11)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    // 後続のビットがある場合
                    if (dcDiffBitLength != 0)
                    {
                        // 後続のビットを読み出し、直流係数と予測値の差分を求める。
                        short dcDiff = (short)ReadBits(dcDiffBitLength);
                        if (dcDiff < _positive[dcDiffBitLength])
                            dcDiff += _makeNegative[dcDiffBitLength];

                        // 直流係数を計算する。この値は次ブロックの直流係数の予測値となる。
                        pred += dcDiff;
                    }
                    // 後続のビットがない場合は、予測値がそのまま直流係数になるので計算不要。

                    if (qDct != null)
                    {
                        // 直流係数を量子化後のDCT係数列格納メモリに代入する。
                        qDct[qDctIndex] = pred;
                    }
                }

                // 交流係数を復号する。
                for (int k = 1; k < 64;)        // ジグザグスキャン順
                {
                    // 交流係数符号表(ゼロ係数の連長と非ゼロ係数の振幅の組み合わせ)を復号する。
                    byte index = acHDT.CodeToIndexLookUpTable[PeepBits(acHDT.BitLengthMax)];
                    if (index > acHDT.IndexMax)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    SkipBits(acHDT.BitLength[index]);

                    byte zeroRun_AcBitLen = acHDT.Symbol[index];
                    byte zeroRun = (byte)(zeroRun_AcBitLen >> 4);
                    byte acBitLength = (byte)(zeroRun_AcBitLen & 0x0F);
                    if (acBitLength > 10)
                        return ReturnID.JpegDataErrorUnexpectedData;  // 予期せぬJPEGデータ

                    // 後続のビットがある場合
                    if (acBitLength != 0)
                    {
                        k += zeroRun;

                        if (qDct != null)
                        {
                            // 後続のビットを読み出し、非ゼロ交流係数を求める。
                            short acVal = (short)ReadBits(acBitLength);
                            if (acVal < _positive[acBitLength])
                                acVal += _makeNegative[acBitLength];

                            // 非ゼロ交流係数を量子化後のDCT係数列格納メモリに代入する。
                            qDct[qDctIndex + k++] = acVal;
                        }
                        else
                        {
                            // 後続のビットをスキップする。
                            SkipBits(acBitLength);
                            k++;
                        }
                    }
                    // 後続のビットがない場合
                    else
                    {
                        if (15 == zeroRun)
                            k += 16;
                        else
                            k = 64;
                    }
                }

                return ReturnID.Success;
            }

            /// <summary>
            /// ビットマスク
            /// </summary>
            private readonly byte[] BIT_MASK = new byte[] { 0x00, 0x01, 0x03, 0x07, 0x0F, 0x1F, 0x3F, 0x7F, 0xFF };

            /// <summary>
            /// 指定したビット数のデータを覗き見る(データポインタは進めない)。
            /// </summary>
            /// <param name="bitLength">ビット数(1〜16)</param>
            /// <returns>指定したビット数のデータ(LSB側に寄せてある)</returns>
            private ushort PeepBits(int bitLength)
            {
                // 注意：0xFF の次に 0x00 がある場合はこの 0x00 を無視しなければならない。

                ushort bits;

                if (bitLength <= _remainBitLength)
                {
                    bits = (ushort)(_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]);
                    bits >>= _remainBitLength - bitLength;
                }
                else if (bitLength <= _remainBitLength + 8)
                {
                    byte next1 = _jpegData[_jpegDataIndex + 1];
                    if ((0xFF == _jpegData[_jpegDataIndex]) && (0 == next1))
                        next1 = _jpegData[_jpegDataIndex + 2];

                    bits = (ushort)(((_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]) << 8) + next1);
                    bits >>= _remainBitLength + 8 - bitLength;
                }
                else
                {
                    byte next1 = _jpegData[_jpegDataIndex + 1];
                    byte next2 = _jpegData[_jpegDataIndex + 2];
                    if ((0xFF == _jpegData[_jpegDataIndex]) && (0 == next1))
                    {
                        next1 = _jpegData[_jpegDataIndex + 2];
                        next2 = _jpegData[_jpegDataIndex + 3];
                        if ((0xFF == next1) && (0 == next2))
                            next2 = _jpegData[_jpegDataIndex + 4];
                    }
                    else if ((0xFF == next1) && (0 == next2))
                        next2 = _jpegData[_jpegDataIndex + 3];

                    uint dwBits = (uint)(((_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]) << 16) + (next1 << 8) + next2);
                    bits = (ushort)(dwBits >> (_remainBitLength + 16 - bitLength));
                }

                return bits;
            }

            /// <summary>
            /// 指定したビット数のデータをスキップする。
            /// </summary>
            /// <param name="bitLength">ビット数(1〜16)</param>
            private void SkipBits(int bitLength)
            {
                // 注意：0xFF の次に 0x00 がある場合はこの 0x00 を無視しなければならない。

                if (bitLength <= _remainBitLength)
                {
                    _remainBitLength -= bitLength;
                }
                else if (bitLength <= _remainBitLength + 8)
                {
                    int increment = ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1])) ? 2 : 1;

                    _jpegDataIndex += increment;
                    _remainSize -= increment;
                    _remainBitLength += 8 - bitLength;
                }
                else
                {
                    int increment;
                    if ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1]))
                        increment = ((0xFF == _jpegData[_jpegDataIndex + 2]) && (0 == _jpegData[_jpegDataIndex + 3])) ? 4 : 3;
                    else
                        increment = ((0xFF == _jpegData[_jpegDataIndex + 1]) && (0 == _jpegData[_jpegDataIndex + 2])) ? 3 : 2;

                    _jpegDataIndex += increment;
                    _remainSize -= increment;
                    _remainBitLength += 16 - bitLength;
                }

                if (_remainBitLength == 0)
                {
                    int increment = ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1])) ? 2 : 1;

                    _jpegDataIndex += increment;
                    _remainSize -= increment;
                    _remainBitLength = 8;
                }
            }

            /// <summary>
            /// 指定したビット数のデータを読み出す。
            /// </summary>
            /// <param name="bitLength">ビット数(1〜16)</param>
            /// <returns>指定したビット数のデータ(LSB側に寄せてある)</returns>
            private ushort ReadBits(int bitLength)
            {
                // 注意：0xFF の次に 0x00 がある場合はこの 0x00 を無視しなければならない。

                ushort bits;

                if (bitLength <= _remainBitLength)
                {
                    bits = (ushort)(_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]);
                    _remainBitLength -= bitLength;
                    bits >>= _remainBitLength;
                }
                else if (bitLength <= _remainBitLength + 8)
                {
                    int increment = ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1])) ? 2 : 1;

                    bits = (ushort)(((_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]) << 8) + _jpegData[_jpegDataIndex + increment]);
                    _remainBitLength += 8 - bitLength;
                    bits >>= _remainBitLength;
                    _jpegDataIndex += increment;
                    _remainSize -= increment;
                }
                else
                {
                    int increment1, increment2;

                    if ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1]))
                    {
                        increment1 = 2;
                        increment2 = ((0xFF == _jpegData[_jpegDataIndex + 2]) && (0 == _jpegData[_jpegDataIndex + 3])) ? 4 : 3;
                    }
                    else
                    {
                        increment1 = 1;
                        increment2 = ((0xFF == _jpegData[_jpegDataIndex + 1]) && (0 == _jpegData[_jpegDataIndex + 2])) ? 3 : 2;
                    }

                    uint dwBits = (uint)(((_jpegData[_jpegDataIndex] & BIT_MASK[_remainBitLength]) << 16) + (_jpegData[_jpegDataIndex + increment1] << 8) + _jpegData[_jpegDataIndex + increment2]);
                    _remainBitLength += 16 - bitLength;
                    bits = (ushort)(dwBits >> _remainBitLength);
                    _jpegDataIndex += increment2;
                    _remainSize -= increment2;
                }

                if (_remainBitLength == 0)
                {
                    int increment = ((0xFF == _jpegData[_jpegDataIndex]) && (0 == _jpegData[_jpegDataIndex + 1])) ? 2 : 1;

                    _jpegDataIndex += increment;
                    _remainSize -= increment;
                    _remainBitLength = 8;
                }

                return bits;
            }

            /// <summary>
            /// 8未満を切り上げてから、8で割った値を計算する。
            /// </summary>
            /// <param name="num">入力</param>
            /// <returns>計算結果</returns>
            private uint CalcUint8Num(uint num)
            {
                return (((num) + 7) >> 3);
            }

            /// <summary>
            /// 16未満を切り上げてから、16で割った値を計算する。
            /// </summary>
            /// <param name="num">入力</param>
            /// <returns>計算結果</returns>
            private uint CalcUint16Num(uint num)
            {
                return (((num) + 15) >> 4);
            }
        }
    }
}
