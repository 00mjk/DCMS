using DCMS.Web.Framework;
using DCMS.Web.Framework.Models;
using System.Collections.Generic;

namespace DCMS.ViewModel.Models.Configuration
{


    public partial class RemarkConfigListModel : BaseEntityModel
    {

        public RemarkConfigListModel()
        {
            PagingFilteringContext = new PagingFilteringModel();
            Lists = new List<RemarkConfigModel>();
        }
        public PagingFilteringModel PagingFilteringContext { get; set; }
        public IList<RemarkConfigModel> Lists { get; set; }
    }


    // [Validator(typeof(RemarkConfigValidator))]
    public partial class RemarkConfigModel : BaseEntityModel
    {


        [HintDisplayName("��ע����", "��ע����")]
        public string Name { get; set; }

        [HintDisplayName("����۸����", "����۸����")]
        public bool RemberPrice { get; set; }
    }
}
