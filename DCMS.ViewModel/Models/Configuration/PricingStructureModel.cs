using DCMS.Web.Framework;
using DCMS.Web.Framework.Models;
using System;
using System.Collections.Generic;


namespace DCMS.ViewModel.Models.Configuration
{


    public partial class PricingStructureListModel : BaseEntityModel
    {

        public PricingStructureListModel()
        {

            Items1 = new List<PricingStructureModel>();
            Items2 = new List<PricingStructureModel>();
        }

        public List<PricingStructureModel> Items1 { get; set; }
        public List<PricingStructureModel> Items2 { get; set; }



        public string ChannelDatas { get; set; }
        public string LevelDatas { get; set; }
        public string TierPricePlanDatas { get; set; }

    }
    /// <summary>
    /// ��ʾ�۸���ϵ����
    /// </summary>
    public partial class PricingStructureModel : BaseEntityModel
    {



        [HintDisplayName("�۸���ϵ���", "�۸���ϵ���")]
        public int PriceType { get; set; } = 0;



        [HintDisplayName("�ͻ�", "�ͻ�")]
        public int CustomerId { get; set; } = 0;


        [HintDisplayName("�ͻ�����", "�ͻ�����")]
        public string CustomerName { get; set; }


        [HintDisplayName("����", "����")]
        public int ChannelId { get; set; } = 0;
        public string ChannelName { get; set; }

        [HintDisplayName("Ƭ��", "Ƭ��")]
        public string DistrictIds { get; set; }
        public string DistrictName { get; set; }



        [HintDisplayName("�ȼ�", "�ȼ�")]
        public int EndPointLevel { get; set; } = 0;
        public string EndPointLevelName { get; set; }


        [HintDisplayName("��ѡ�۸�", "��ѡ�۸�")]
        public string PreferredPrice { get; set; }
        public string PreferredPriceName { get; set; }


        [HintDisplayName("��ѡ�۸�", "��ѡ�۸�")]
        public string SecondaryPrice { get; set; }
        public string SecondaryPriceName { get; set; }

        [HintDisplayName("ĩѡ�۸�", "ĩѡ�۸�")]
        public string FinalPrice { get; set; }
        public string FinalPriceName { get; set; }

        [HintDisplayName("����ʱ��", "����ʱ��")]
        public DateTime CreatedOnUtc { get; set; }


        [HintDisplayName("Ȩ��", "Ȩ��")]
        public int Order { get; set; } = 0;
    }
}
