using DCMS.Web.Framework;
using DCMS.Web.Framework.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Xml.Serialization;
using DCMS.Core;


namespace DCMS.ViewModel.Models.Configuration
{


    public class PrintTemplateListModel : BaseEntityModel
    {

        public PrintTemplateListModel()
        {
            PagingFilteringContext = new PagingFilteringModel();
            Lists = new List<PrintTemplateModel>();
        }
        public PagingFilteringModel PagingFilteringContext { get; set; }
        public IList<PrintTemplateModel> Lists { get; set; }
    }


    // [Validator(typeof(PrintTemplateValidator))]
    public class PrintTemplateModel : BaseEntityModel
    {

        [HintDisplayName("ģ������", "ģ������")]
        public int? TemplateType { get; set; } = 0;
        public string TemplateTypeName { get; set; }
        [XmlIgnore]
        public SelectList TemplateTypes { get; set; }

        [HintDisplayName("��������", "��������")]
        public int? BillType { get; set; } = 0;
        public string BillTypeName { get; set; }
        [XmlIgnore]
        public SelectList BillTypes { get; set; }

        [HintDisplayName("����", "����")]
        public string Title { get; set; }

        [HintDisplayName("����", "����")]
        public string Content { get; set; }

        [HintDisplayName("ֽ������", "ֽ������")]
        public int PaperType { get; set; }
        public SelectList PaperTypes { get; set; }
        public PaperTypeEnum EPaperTypes
        {
            get => (PaperTypeEnum)PaperType;
            set => PaperType = (int)value;
        }

        [HintDisplayName("ֽ�ſ��", "ֽ�ſ��")]
        public double PaperWidth { get; set; }

        [HintDisplayName("ֽ�Ÿ߶�", "ֽ�Ÿ߶�")]
        public double PaperHeight { get; set; }

    }
}
