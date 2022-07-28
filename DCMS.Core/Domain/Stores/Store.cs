using System;
using System.ComponentModel.DataAnnotations.Schema;


namespace DCMS.Core.Domain.Stores
{
    /*
    /// <summary>
    /// ��ʾһ������(���ǵľ�����)
    /// </summary>
    public partial class Store : BaseEntity
    {
        /// <summary>
        /// ��������
        /// </summary>
        public int? BranchId { get; set; } = 0;

        /// <summary>
        /// ʶ����
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// ����������
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ������URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// �Ƿ�����SSL
        /// </summary>
        [Column(TypeName = "BIT(1)")]
        public bool SslEnabled { get; set; }

        /// <summary>
        /// ��ȫURL (HTTPS)
        /// </summary>
        public string SecureUrl { get; set; }

        /// <summary>
        /// ���ŷָ���HTTP_HOST�б� 
        /// </summary>
        public string Hosts { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// ����ʱ��
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// �Ƿ�����
        /// </summary>
        [Column(TypeName = "BIT(1)")]
        public bool Published { get; set; }


        /// <summary>
        /// �Ǽ���
        /// </summary>
        public int StarRate { get; set; } = 0;


        /// <summary>
        /// ERP�����̱��
        /// </summary>
        public string DealerNumber { get; set; }

        /// <summary>
        /// ERPӪ������
        /// </summary>
        public string MarketingCenter { get; set; }
        public string MarketingCenterCode { get; set; }

        /// <summary>
        /// ERP���۴���
        /// </summary>
        public string SalesArea { get; set; }
        public string SalesAreaCode { get; set; }

        /// <summary>
        /// ERPҵ��
        /// </summary>
        public string BusinessDepartment { get; set; }
        public string BusinessDepartmentCode { get; set; }

        /// <summary>
        /// �Ƿ񼤻��װ��ʼ�򵼻���־Ϊ��1��
        /// </summary>
        [Column(TypeName = "BIT(1)")]
        public bool Actived { get; set; }

        [Column(TypeName = "BIT(1)")]
        public bool Setuped { get; set; }
    }
    */


    /// <summary>
    /// �����̣������̣�������Ϣ�ṹ
    /// </summary>
    //[Table("CRM_Stores")]
    public class Store : BaseEntity
    {

        /// <summary>
        /// �������
        /// </summary>
        public string QuyuCode { get; set; } = "";

        #region DCMS

        /// <summary>
        /// ��������
        /// </summary>
        public int BranchId { get; set; } = 0;

        /// <summary>
        /// ʶ����
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// ����������
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// ������URL
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// �Ƿ�����SSL
        /// </summary>
        //[Column("SslEnabled", "BIT(1)")]
        public bool SslEnabled { get; set; }

        /// <summary>
        /// ��ȫURL (HTTPS)
        /// </summary>
        public string SecureUrl { get; set; } = "";

        /// <summary>
        /// ���ŷָ���HTTP_HOST�б� 
        /// </summary>
        public string Hosts { get; set; } = "";

        /// <summary>
        /// ����
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// ����ʱ��
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// �Ƿ�����
        /// </summary>
        //[Column("Published", "BIT(1)")]
        public bool Published { get; set; }


        /// <summary>
        /// �Ǽ���
        /// </summary>
        public int StarRate { get; set; } = 0;


        /// <summary>
        /// ERP�����̱��
        /// </summary>
        public string DealerNumber { get; set; } = "";

        /// <summary>
        /// ERPӪ������
        /// </summary>
        public string MarketingCenter { get; set; } = "";
        public string MarketingCenterCode { get; set; } = "";

        /// <summary>
        /// ERP���۴���
        /// </summary>
        public string SalesArea { get; set; } = "";
        public string SalesAreaCode { get; set; } = "";

        /// <summary>
        /// ERPҵ��
        /// </summary>
        public string BusinessDepartment { get; set; } = "";
        public string BusinessDepartmentCode { get; set; } = "";

        /// <summary>
        /// �Ƿ񼤻��װ��ʼ�򵼻���־Ϊ��1��
        /// </summary>
        public bool Actived { get; set; }

        /// <summary>
        /// �Ƿ��Ѿ���ʼ��
        /// </summary>
        public bool Setuped { get; set; }

        #endregion

        #region CRM

        /// <summary>
        /// ҵ������
        /// </summary>
        public string PARTNER { get; set; }

        /// <summary>
        /// ������Ի�ϵͳ���
        /// </summary>
        public string ZZFLD0000CF { get; set; }

        ///// <summary>
        ///// ��֯����
        ///// </summary>
        //public string NAME_ORG1 { get; set; }

        /// <summary>
        /// ���Ĺ鵵��־
        /// </summary>
        public string XDELE { get; set; }

        /// <summary>
        /// ��ϵ��
        /// </summary>
        public string ZZPERSON { get; set; }

        /// <summary>
        /// ��ϵ�˵绰
        /// </summary>
        public string ZZTELPHONE { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string REGION { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string ZZCITY { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string ZZCOUNTY { get; set; }

        /// <summary>
        /// �ֵ�
        /// </summary>
        public string ZZSTREET_NUM { get; set; }

        /// <summary>
        /// ��
        /// </summary>
        public string ZZLILLAGE_NUM { get; set; }

        /// <summary>
        /// ��ϸ��ַ
        /// </summary>
        public string ZZADD_DETAIL { get; set; }

        /// <summary>
        /// �ͻ�����
        /// </summary>
        public string ZZQUDAO_TYPE { get; set; }

        /// <summary>
        /// ����������
        /// </summary>
        public string ZZCLIENT_TYPE { get; set; }

        /// <summary>
        /// �ն˺�������
        /// </summary>
        public string ZZFLD00005V { get; set; }

        /// <summary>
        /// ����ģʽ
        /// </summary>
        public string ZZDILIVER_MODEL { get; set; }

        /// <summary>
        /// ��ҵ����
        /// </summary>
        public string ZZRUN_CHARA { get; set; }

        /// <summary>
        /// ���֤��
        /// </summary>
        public string ZZCARDID { get; set; }

        /// <summary>
        /// ͳһ������ô���
        /// </summary>
        public string ZZBUSINESS { get; set; }

        /// <summary>
        /// ˰��Ǽ�֤���
        /// </summary>
        public string ZZTAX { get; set; }

        /// <summary>
        /// ����ר���������֤
        /// </summary>
        public string ZZALCOHOL { get; set; }

        /// <summary>
        /// ҵ����Ա����
        /// </summary>
        public string ZZMANAGEMENT { get; set; }

        /// <summary>
        /// רְ�ͻ���������
        /// </summary>
        public string ZZFULLTIME { get; set; }

        /// <summary>
        /// �̶��ִ������ƽ�ף�
        /// </summary>
        public string ZZSTORAGE { get; set; }

        /// <summary>
        /// �ֿ�����
        /// </summary>
        public string ZZWAREHOUSE1 { get; set; }

        /// <summary>
        /// ����������
        /// </summary>
        public string ZZCAR { get; set; }

        /// <summary>
        /// �ǻ���������
        /// </summary>
        public string ZZNONCAR { get; set; }

        /// <summary>
        /// �Ƿ񵱵��ص����Ʒһ����
        /// </summary>
        public string ZZFLD0000CH { get; set; }

        /// <summary>
        /// ����Ʒ����ϸ
        /// </summary>
        public string ZZWILLINGNESS { get; set; }

        /// <summary>
        /// ��ᱳ��
        /// </summary>
        public string ZZBACK_GROUND { get; set; }

        /// <summary>
        /// ��ᱳ��
        /// </summary>
        public string ZZBACKGROUND { get; set; }

        /// <summary>
        /// ��������
        /// </summary>
        public string ZZCOMMENT { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string ZZOFFICE_ID { get; set; }

        /// <summary>
        /// ���´�
        /// </summary>
        public string ZZGROUP_ID { get; set; }

        /// <summary>
        /// ����վ
        /// </summary>
        public string ZZGZZ_ID { get; set; }

        /// <summary>
        /// ����ʱ��
        /// </summary>
        public string ZDATE { get; set; }

        #endregion
    }
}
