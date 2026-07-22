using System;
namespace HOWTOUSE.DTO.Common
{
    public class CommonCodeDto
    {
        public string ComnGrpCd { get; set; }
        public string ComnCd { get; set; }
        public string ComnCdNm { get; set; }
        public string ComnCdExpl { get; set; }
        public int ScrnMrkSeq { get; set; }
        public string UseYn { get; set; }
        public string Dtrl1Nm { get; set; }
        public string Dtrl2Nm { get; set; }
        public string Dtrl3Nm { get; set; }
        public string Dtrl4Nm { get; set; }
        public string FsrStfNo { get; set; }
        public DateTime FsrDtm { get; set; }
        public string LshStfNo { get; set; }
        public DateTime LshDtm { get; set; }

        public override string ToString()
        {
            return ComnCdNm;
        }
    }
}
