using System;
using System.Collections.Generic;

namespace HOWTOUSE.DTO.PopupManual
{
    public class PopupManual_INOUT
    {
        public int ManuNo { get; set; }
        public string CategoryCd { get; set; }
        public string ManualNm { get; set; }
        public string MessageCnte { get; set; }
        public string ProblemCnte { get; set; }
        public string AskStfNm { get; set; }
        public string TelNo { get; set; }
        public string FsrStfNo { get; set; }
        public DateTime FsrDtm { get; set; }
        public string LshStfNo { get; set; }
        public DateTime LshDtm { get; set; }
        public List<PopupManualStepDto> Steps { get; set; } = new List<PopupManualStepDto>();
        public List<PopupManualImageDto> Images { get; set; } = new List<PopupManualImageDto>();
        public List<PopupManualKeywordDto> Keywords { get; set; } = new List<PopupManualKeywordDto>();
    }
}
