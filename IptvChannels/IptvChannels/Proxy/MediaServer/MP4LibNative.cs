using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class MP4LibNative
    {
        [DllImport("mp4lib.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern MP4_API_RESULT Decrypt(string strInputFilename, string strOutputFilename, string strFragmentsInfoFilename, byte[] kid, byte[] key, uint iTrackID);

        [DllImport("mp4lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern MP4_API_RESULT Decrypt(byte[] inputFilename, byte[] outputFilename, byte[] fragmentsInfoFilename, byte[] kid, byte[] key, uint iTrackID);

        [DllImport("mp4lib.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern MP4_API_RESULT Extract(byte[] pInputFilename, byte[] atom_path, byte[] buffer, int iBufferSize, bool bPayloadOnly, out int iOutputSize);

        [DllImport("mp4lib.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern MP4_API_RESULT ExtractFromSegment(byte[] pInputFilename, byte[] pBufferPssh, byte[] pBufferKid, int iBufferPsshSize, int iBufferKidSize,
            out int pOutPsshSize, out int pOutKidSize);

        public enum MP4_API_RESULT
        {
            AP4_SUCCESS = 0,
            AP4_FAILURE = -1,
            AP4_ERROR_OUT_OF_MEMORY = -2,
            AP4_ERROR_INVALID_PARAMETERS = -3,
            AP4_ERROR_NO_SUCH_FILE = -4,
            AP4_ERROR_PERMISSION_DENIED = -5,
            AP4_ERROR_CANNOT_OPEN_FILE = -6,
            AP4_ERROR_EOS = -7,
            AP4_ERROR_WRITE_FAILED = -8,
            AP4_ERROR_READ_FAILED = -9,
            AP4_ERROR_INVALID_FORMAT = -10,
            AP4_ERROR_NO_SUCH_ITEM = -11,
            AP4_ERROR_OUT_OF_RANGE = -12,
            AP4_ERROR_INTERNAL = -13,
            AP4_ERROR_INVALID_STATE = -14,
            AP4_ERROR_LIST_EMPTY = -15,
            AP4_ERROR_LIST_OPERATION_ABORTED = -16,
            AP4_ERROR_INVALID_RTP_CONSTRUCTOR_TYPE = -17,
            AP4_ERROR_NOT_SUPPORTED = -18,
            AP4_ERROR_INVALID_TRACK_TYPE = -19,
            AP4_ERROR_INVALID_RTP_PACKET_EXTRA_DATA = -20,
            AP4_ERROR_BUFFER_TOO_SMALL = -21,
            AP4_ERROR_NOT_ENOUGH_DATA = -22,

            AP4_ERROR_INVAILD_INPUT_FILE_PARAMETRER = -23,
            AP4_ERROR_INVAILD_OUTPUT_FILE_PARAMETRER = -24,
            AP4_ERROR_INVAILD_KID_PARAMETRER = -25,
            AP4_ERROR_INVAILD_KEY_PARAMETRER = -26,
            AP4_ERROR_CANNOT_OPEN_INPUT_FILE = -27,
            AP4_ERROR_CANNOT_CREATE_OUTPUT_FILE = -28,
            AP4_ERROR_CANNOT_OPEN_FRAGMENT_FILE = -29,
            AP4_ERROR_INVALID_ATOM_PATH_PARAMETER = -30,
            AP4_ERROR_ATOM_NOT_FOUND = -31,
            AP4_ERROR_INVAILD_BUFFER_PARAMETRER = -32,
        }
    }
}
