using DCMS.Core.Domain.Terminals;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCMS.Services.Terminals
{
    /// <summary>
    /// Ƭ����չ
    /// </summary>
    public static class DistrictExtensions
    {

        /// <summary>
        /// ���������
        /// </summary>
        /// <param name="source">Դ</param>
        /// <param name="parentId">���ڵ�</param>
        /// <param name="ignoreCategoriesWithoutExistingParent">�Ƿ���Բ����ڵĸ���</param>
        /// <returns></returns>
        public static IList<District> SortDistrictForTree(this IList<District> source, int parentId = 0, bool ignoreCategoriesWithoutExistingParent = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var result = new List<District>();

            foreach (var cat in source.ToList().FindAll(c => c.ParentId == parentId))
            {
                result.Add(cat);
                result.AddRange(SortDistrictForTree(source, cat.Id, ignoreCategoriesWithoutExistingParent));
            }
            if (!ignoreCategoriesWithoutExistingParent && result.Count != source.Count)
            {
                foreach (var cat in source)
                {
                    if (result.FirstOrDefault(x => x.Id == cat.Id) == null)
                    {
                        if (cat.Id == parentId)
                        {
                            result.Add(cat);
                        }
                    }
                }
            }
            return result;
        }



    }
}
