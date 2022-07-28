using DCMS.Core;
using DCMS.Core.Caching;
using DCMS.Core.Domain.Common;
using DCMS.Core.Domain.Terminals;
using DCMS.Core.Domain.Users;
using DCMS.Core.Infrastructure.DependencyManagement;
using DCMS.Services.Caching;
using DCMS.Services.Events;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using DCMS.Core.Domain.CRM;
using Newtonsoft.Json;

namespace DCMS.Services.Terminals
{
    /// <summary>
    ///  �ն���Ϣ����
    /// </summary>
    public partial class TerminalService : BaseService, ITerminalService
    {
        public TerminalService(IServiceGetter getter,
            IStaticCacheManager cacheManager,

            IEventPublisher eventPublisher) : base(getter, cacheManager, eventPublisher)
        {

        }

        /// <summary>
        /// ��ȡȫ���ն���Ϣ
        /// </summary>
        /// <returns></returns>
        public virtual IList<Terminal> GetAllTerminal(int? storeId)
        {
            return _cacheManager.Get(DCMSDefaults.TERMINAL_ALL_STORE_KEY.FillCacheKey(storeId), () =>
            {
                var query = from c in TerminalsRepository_RO.TableNoTracking
                            where !c.Deleted
                            select c;

                if (storeId != null)
                {
                    query = query.Where(c => c.StoreId == storeId);
                }

                query = query.OrderByDescending(c => c.CreatedOnUtc);
                return query.ToList();
            });
        }

        /// <summary>
        /// ��ȡ�ն��б�
        /// </summary>
        /// <param name="searchStr"></param>
        /// <param name="storeId">������Id</param>
        /// <param name="channelId"></param>
        /// <param name="rank"></param>
        /// <param name="status"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Terminal> GetTerminals(int? storeId,
            int? userId,
            IList<int> districtIds,
            string searchStr,
            int? channelId,
            int? rankId,
            int? lineId = 0,
            bool? status = true,
            double lat = 0,
            double lng = 0,
            double range = 1.5,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            List<Terminal> latLngQueryList = new List<Terminal>();

            //���pageSizeΪ-1�����Ʋ�ѯ
            if (pageSize == -1)
            {
                pageSize = int.MaxValue;
            }

            searchStr = CommonHelper.FilterSQLChar(searchStr);


            var tdistrictIds = new List<int>();
            if (districtIds != null)
                tdistrictIds = districtIds.ToList();


            #region Ƭ��

            if (userId.HasValue)
            {
                //��ȡ��ǰ�û�����Ƭ��
                var curUserDistricts = UserDistrictsRepository.Table
                    .Where(s => s.StoreId == storeId && s.UserId == userId)
                    .OrderBy(s => s.DistrictId)
                    .Select(s => s.DistrictId)
                    .ToList();

                var ids = curUserDistricts.Where(s => s > 0)?.ToList();
                if (ids != null && ids.Count > 0)
                {
                    tdistrictIds.AddRange(ids);
                }
            }

            #endregion

            tdistrictIds = tdistrictIds.Where(s => s != 0).ToList();



            var query = (from t in TerminalsRepository.Table
                         where t.StoreId == storeId && !t.Deleted
                         select t)
                         .OrderByDescending(s => s.Id).AsQueryable();


            var subordinates = new List<int>();

            //ָ����Ƭ��
            if (tdistrictIds.Any())
            {
                ///////////////////2021-10-09 mu �޸�///////////////////////////
                var prentDistricts = DistrictsRepository.Table.Where(w => tdistrictIds.Contains(w.Id)).Select(s => s.ParentId).Distinct().ToList();
                tdistrictIds.AddRange(prentDistricts);
                //if (!tdistrictIds.Contains(0)) tdistrictIds.Add(0);

                if (userId != null && userId > 0)
                {
                    var userIds = GetSubordinate(storeId, userId ?? 0)?.Where(s => s > 0).ToList();
                    query = query.Where(c => tdistrictIds.Distinct().Contains(c.DistrictId) || (userIds.Contains(c.CreatedUserId) && c.DistrictId == 0));
                }
                else
                {
                    query = query.Where(c => tdistrictIds.Distinct().Contains(c.DistrictId));
                }
                ///////////////////////////////////////////////////////////////
            }
            else
            {
                if (userId != null && userId > 0)
                {
                    var userIds = GetSubordinate(storeId, userId ?? 0)?.Where(s => s > 0).ToList();
                    if (userIds.Count > 0)
                        query = query.Where(x => userIds.Contains(x.CreatedUserId) || x.CreatedUserId >= 0);
                    else
                        query = query.Where(x => x.CreatedUserId == userId || x.CreatedUserId >= 0);
                }
            }

            if (!string.IsNullOrEmpty(searchStr) && !searchStr.Equals("null"))
            {
                query = query.Where(l => !string.IsNullOrEmpty(l.Name) && (l.Name.Contains(searchStr) || l.Name.StartsWith(searchStr) || l.Name.EndsWith(searchStr)) || !string.IsNullOrEmpty(l.BossCall) && (l.BossCall.Contains(searchStr) || l.BossCall.StartsWith(searchStr) || l.BossCall.EndsWith(searchStr)));
                query = query.OrderByDescending(s => s.Name);
            }

            if (channelId != null && channelId > 0)
            {
                query = query.Where(t => t.ChannelId == channelId);
            }

            if (rankId != null && rankId > 0)
            {
                query = query.Where(t => t.RankId == rankId);
            }

            if (lineId != null && lineId > 0)
            {
                query = query.Where(t => t.LineId == lineId);
            }

            if (status != null)
            {
                query = query.Where(t => t.Status == status);
            }

            searchStr = searchStr?.Replace("null", "");

            if (lat > 0 && lng > 0 && range > 0 && string.IsNullOrEmpty(searchStr))
            {
                var latLngQuery = query.Where(t => Math.Sqrt(
                    (
     ((lng - t.Location_Lng) * Math.PI * 12656 * Math.Cos(((lat + t.Location_Lat) / 2) * Math.PI / 180) / 180)
     *
     ((lng - t.Location_Lng) * Math.PI * 12656 * Math.Cos(((lat + t.Location_Lat) / 2) * Math.PI / 180) / 180)
    )
    +
    (
     ((lat - t.Location_Lat) * Math.PI * 12656 / 180)
     *
     ((lat - t.Location_Lat) * Math.PI * 12656 / 180)
    )
                    ) < range);

                var gpsLists = latLngQuery.ToList();
                if (gpsLists != null && gpsLists.Any())
                {
                    //��ҳ��
                    var temps = query.ToList();
                    gpsLists.AddRange(temps);
                    gpsLists = gpsLists.DistinctBy(p => p.Id).ToList();
                    var totalCount = gpsLists.Count();
                    var plists = gpsLists.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                    return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
                }
                else
                {
                    //��ҳ��
                    var totalCount = query.Count();
                    var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                    return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
                }
            }
            else
            {
                //��ҳ��
                var totalCount = query.Count();
                var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
                return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
            }
        }


        /// <summary>
        /// ��ȡ�ն��б�
        /// </summary>
        /// <param name="searchStr"></param>
        /// <param name="storeId">������Id</param>
        /// <param name="channelId"></param>
        /// <param name="rank"></param>
        /// <param name="status"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Terminal> GetTerminals(int? storeId,
            int? userId,
            IList<int> districtIds,
            string searchStr,
            int? channelId,
            int? rankId,
            int? lineId = 0,
            bool? status = true,
            bool isWeb = false,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            //���pageSizeΪ-1�����Ʋ�ѯ
            if (pageSize == -1)
            {
                pageSize = int.MaxValue;
            }

            searchStr = CommonHelper.FilterSQLChar(searchStr);


            var tdistrictIds = new List<int>();
            if (districtIds != null)
                tdistrictIds = districtIds.ToList();


            #region Ƭ��

            if (userId.HasValue && userId > 0)
            {
                //��ȡ��ǰ�û�����Ƭ��
                var curUserDistricts = UserDistrictsRepository.Table
                    .Where(s => s.StoreId == storeId && s.UserId == userId)
                    .OrderBy(s => s.DistrictId)
                    .Select(s => s.DistrictId)
                    .ToList();

                var ids = curUserDistricts.Where(s => s > 0)?.ToList();
                if (ids != null && ids.Count > 0)
                {
                    tdistrictIds.AddRange(ids);
                }
            }

            #endregion
            tdistrictIds = tdistrictIds.Where(s => s != 0).ToList();

            var query = (from t in TerminalsRepository.Table
                         where t.StoreId == storeId && !t.Deleted
                         select t)
                         .OrderByDescending(s => s.Id).AsQueryable();


            if (userId != null && userId > 0)
            {
                var userIds = GetSubordinate(storeId, userId ?? 0)?.Where(s => s > 0).ToList();
                if (userIds.Count > 0)
                    query = query.Where(x => userIds.Contains(x.CreatedUserId) || x.CreatedUserId >= 0);
                else
                    query = query.Where(x => x.CreatedUserId == userId || x.CreatedUserId >= 0);
            }


            //ָ����Ƭ��
            if (tdistrictIds.Any())
            {
                if (tdistrictIds.Count == 1 && tdistrictIds[0] == 0)
                {
                    if (userId != null && userId > 0)
                    {
                        var userIds = GetSubordinate(storeId, userId ?? 0)?.Where(s => s > 0).ToList();
                        if (userIds.Count > 0)
                            query = query.Where(x => userIds.Contains(x.CreatedUserId));
                        else
                            query = query.Where(x => x.CreatedUserId == userId);
                    }
                }
                else
                {
                    var prentDistricts = DistrictsRepository.Table.Where(w => tdistrictIds.Contains(w.Id)).Select(s => s.ParentId).Distinct().ToList();
                    tdistrictIds.AddRange(prentDistricts);
                    if (isWeb)
                    {
                        tdistrictIds.Remove(0);
                    }
                    else
                    {
                        if (!tdistrictIds.Contains(0)) tdistrictIds.Add(0);
                    }
                    if (userId != null && userId > 0)
                    {
                        var userIds = GetSubordinate(storeId, userId ?? 0)?.Where(s => s > 0).ToList();
                        query = query.Where(c => tdistrictIds.Distinct().Contains(c.DistrictId) || (userIds.Contains(c.CreatedUserId) && c.DistrictId == 0));
                    }
                    else
                    {
                        query = query.Where(c => tdistrictIds.Distinct().Contains(c.DistrictId));
                    }
                }
            }

            if (!string.IsNullOrEmpty(searchStr) && !searchStr.Equals("null"))
            {
                query = query.Where(l => !string.IsNullOrEmpty(l.Name) && (l.Name.Contains(searchStr) || l.Name.StartsWith(searchStr) || l.Name.EndsWith(searchStr)) || !string.IsNullOrEmpty(l.BossCall) && (l.BossCall.Contains(searchStr) || l.BossCall.StartsWith(searchStr) || l.BossCall.EndsWith(searchStr)));
                query = query.OrderByDescending(s => s.Name);
            }

            if (channelId != null && channelId > 0)
            {
                query = query.Where(t => t.ChannelId == channelId);
            }

            if (rankId != null && rankId > 0)
            {
                query = query.Where(t => t.RankId == rankId);
            }

            if (lineId != null && lineId > 0)
            {
                query = query.Where(t => t.LineId == lineId);
            }

            if (status != null)
            {
                query = query.Where(t => t.Status == status);
            }

            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
        }

        /// <summary>
        /// ��ȡ�ն��б�
        /// </summary>
        /// <param name="searchStr"></param>
        /// <param name="storeId">������Id</param>
        /// <param name="channelId"></param>
        /// <param name="rank"></param>
        /// <param name="status"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Terminal> GetAllTerminals(int? storeId,
            int? userId,
            IList<int> districtIds,
            string searchStr,
            int? channelId,
            int? rankId,
            double lat = 0,
            double lng = 0,
            double range = 0.5,
            IList<int> lineIds = null,
            bool? status = true,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            //���pageSizeΪ-1�����Ʋ�ѯ
            if (pageSize == -1)
            {
                pageSize = int.MaxValue;
            }

            searchStr = CommonHelper.FilterSQLChar(searchStr);

            var strWhere = $" WHERE StoreId={storeId} AND T.Deleted=0";

            var strSql = @"SELECT * FROM(SELECT '' AS QuyuCode,T.Id,T.StoreId,T.`Name`,T.MnemonicName,T.BossName,T.BossCall,T.`Status`,T.MaxAmountOwed,T.`Code`,T.Address,
                            T.Remark,T.DistrictId,T.ChannelId,T.LineId,T.RankId,T.PaymentMethod,T.Location_Lng,T.Location_Lat,T.BusinessNo,
                            T.FoodBusinessLicenseNo,T.EnterpriseRegNo,T.Deleted,T.CreatedUserId,T.CreatedOnUtc,T.DataTypeId,T.DoorwayPhoto,
                            T.Related,T.IsAgreement,T.Cooperation,T.IsDisplay,T.IsVivid,T.IsPromotion,T.OtherRamark,T.IsNewAdd,T.PARTNER,
                            T.ZZSTATUS1,T.ZZVIRTUAL,T.TEL_NUMBER,T.REGION,T.ZZCITY,T.ZZCOUNTY,T.ZZSTREET_NUM,T.POST_CODE1,T.ZZSTORE_TYPE1,
                            T.ZZSTORE_TYPE2,T.ZZSTORE_TYPE3,T.ZZSTORE_TYP4,T.ZZFLD00005V,T.ZZGDFL,T.ZZBN_TYPE,T.ZZVISIT,T.ZZAGE,T.ZZWHET_CHAIN,
                            T.ZZINNER_AREA,T.ZZOPEN_TIME,T.ZZGEO,T.ZZCHARACTER,T.ZZBOX,T.ZZTABLE,T.CRDAT,T.CRTIM,T.CHDAT,T.CHTIM,T.ZZDIRECTOR,
                            T.ZZSALESMAN,T.ZZOFFICE_ID,T.ZZGROUP_ID,T.ZZGZZ_ID,T.ZZGDFJ
                            ,round(6378.138*2*asin(sqrt(pow(sin((T.Location_Lat*pi()/180-{1}*pi()/180)/2),2)+cos(T.Location_Lat*pi()/180)
                            *cos({1}*pi()/180)*pow(sin((T.Location_Lng*pi()/180-{2}*pi()/180)/2),2)))*1000)/1000 As Distance
                            FROM CRM_Terminals T {0}) T1 ";

            if (districtIds?.Count > 0)  //ָ����Ƭ����ѯ����Ҳ���Բ�ѯδָ��Ƭ�����ն�
            {
                if (!districtIds.Contains(0)) districtIds.Add(0);
                strWhere += $" and T.DistrictId IN({string.Join(",", districtIds)})";
            }

            if (channelId != null && channelId > 0)
            {
                strWhere += $" and T.ChannelId={channelId}";
            }

            if (rankId != null && rankId > 0)
            {
                strWhere += $" and T.RankId={rankId}";
            }

            if (lineIds != null && lineIds.Count() > 0)
            {
                //��ȡָ����·���ն�
                var qeury_Terminal = from l in LineTierOptionsRepository.Table
                                     where lineIds.Contains(l.LineTierId) && l.StoreId == storeId.Value
                                     select l.TerminalId;
                var lst = qeury_Terminal.ToList();
                if (lst?.Count == 0) lst.Add(0);
                strWhere += $" and T.Id In({string.Join(",", lst)})";
            }

            if (status != null)
            {
                strWhere += $" and T.Status={status}";
            }

            searchStr = searchStr == "null" ? "" : searchStr;

            if (!string.IsNullOrEmpty(searchStr))
            {
                strWhere += $" and (T.`Name` LIKE '%{searchStr}%' OR T.BossCall LIKE '%{searchStr}%')";
            }

            var strWhere1 = string.Empty;
            if (range != null && range > 0 && string.IsNullOrEmpty(searchStr))
            {
                strWhere1 += $" WHERE T1.Distance<={range}";
            }
            //��ȡ���������
            var strCount = $"{string.Format(strSql, strWhere, lat, lng)}{strWhere1}";
            //��ѯ�������
            var strQuery = $"{string.Format(strSql, strWhere, lat, lng)}{strWhere1} Order By T1.Distance limit {pageIndex * pageSize},{pageSize}";

            var totalCount = TerminalsRepository.QueryFromSql<Terminal>(strCount).Count();

            var plists = TerminalsRepository.QueryFromSql<Terminal>(strQuery).ToList();

            //var query = (from t in TerminalsRepository.Table
            //             where t.StoreId == storeId && !t.Deleted
            //             select t)
            //             .OrderByDescending(s => s.Id).AsQueryable();

            //if (districtIds?.Count > 0 && !districtIds.Contains(0))  //ָ����Ƭ����ѯ����Ҳ���Բ�ѯδָ��Ƭ�����ն�
            //{
            //    districtIds.Add(0);
            //    query = query.Where(w => districtIds.Contains(w.DistrictId));
            //}

            //if (channelId != null && channelId > 0)
            //{
            //    query = query.Where(t => t.ChannelId == channelId);
            //}

            //if (rankId != null && rankId > 0)
            //{
            //    query = query.Where(t => t.RankId == rankId);
            //}

            //if (lineId != null && lineId > 0)
            //{
            //    query = query.Where(t => t.LineId == lineId);
            //}

            //if (status != null)
            //{
            //    query = query.Where(t => t.Status == status);
            //}
            //if (!string.IsNullOrEmpty(searchStr) && !searchStr.Equals("null"))
            //{
            //    query = query.Where(l => !string.IsNullOrEmpty(l.Name) && (l.Name.Contains(searchStr) || l.Name.StartsWith(searchStr) || l.Name.EndsWith(searchStr)) || !string.IsNullOrEmpty(l.BossCall) && (l.BossCall.Contains(searchStr) || l.BossCall.StartsWith(searchStr) || l.BossCall.EndsWith(searchStr)));
            //    query = query.OrderByDescending(s => s.Name);
            //}
            //else 
            //{
            //    if (lat > 0 && lng > 0 && range > 0)
            //    {
            //        query = query.Where(t => Math.Sqrt(
            //            (
            //                 ((lng - t.Location_Lng) * Math.PI * 12656 * Math.Cos(((lat + t.Location_Lat) / 2) * Math.PI / 180) / 180)
            //                 *
            //                 ((lng - t.Location_Lng) * Math.PI * 12656 * Math.Cos(((lat + t.Location_Lat) / 2) * Math.PI / 180) / 180)
            //                )
            //                +
            //                (
            //                 ((lat - t.Location_Lat) * Math.PI * 12656 / 180)
            //                 *
            //                 ((lat - t.Location_Lat) * Math.PI * 12656 / 180)
            //                )
            //            ) < range);
            //    }
            //}
            //��ҳ��
            //var totalCount = query.Count();
            //var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
        }

        private List<int> GetSubordinate(int? store, int userId)
        {
            var ids = new List<int>();
            try
            {
                var query = UserRepository.Table;
                query = query.Where(c => c.Id == userId && c.StoreId == store.Value);
                var subs = query.Select(s => s.Subordinates).FirstOrDefault();
                if (!string.IsNullOrEmpty(subs))
                {
                    ids = JsonConvert.DeserializeObject<List<int>>(subs);
                }

                if (ids != null)
                    ids.Add(userId);

                return ids;
            }
            catch (Exception)
            {
                if (ids != null)
                    ids.Add(userId);

                return ids;
            }

        }

        /// <summary>
        /// �ͻ�������ѡ��
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="searchStr"></param>
        /// <param name="status"></param>
        /// <param name="currentUser"></param>
        /// <param name="salesmanOnlySeeHisCustomer"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual IPagedList<Terminal> GetPopupTerminals(int? storeId, string searchStr, bool? status, User currentUser, bool salesmanOnlySeeHisCustomer = false, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;

            //var query = (from a in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on a.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select new Terminal
            //             {
            //                 Id = a.Id,
            //                 DataTypeId = (int)TerminalDataType.Terminal,
            //                 Name = a.Name,
            //                 BossName = a.BossName,
            //                 BossCall = string.IsNullOrEmpty(a.BossCall) ? a.TEL_NUMBER : a.BossCall,//
            //                 ChannelId = a.ChannelId,
            //                 Address = a.Address,
            //                 MaxAmountOwed = a.MaxAmountOwed,
            //                 StoreId = a.StoreId,
            //                 Status = a.Status,
            //                 DistrictId = a.DistrictId
            //             }).OrderByDescending(s => s.Id).AsQueryable();

            var query = from a in TerminalsRepository_RO.Table
                        where a.Deleted == false && a.StoreId == storeId
                        select new Terminal
                        {
                            Id = a.Id,
                            DataTypeId = (int)TerminalDataType.Terminal,
                            Name = a.Name,
                            BossName = a.BossName,
                            BossCall = string.IsNullOrEmpty(a.BossCall) ? a.TEL_NUMBER : a.BossCall,//
                            ChannelId = a.ChannelId,
                            Address = a.Address,
                            MaxAmountOwed = a.MaxAmountOwed,
                            StoreId = a.StoreId,
                            Status = a.Status,
                            DistrictId = a.DistrictId,
                            CreatedUserId = a.CreatedUserId
                        };



            if (!string.IsNullOrEmpty(searchStr))
            {
                query = query.Where(q => q.Name.Contains(searchStr));
            }

            if (status != null)
            {
                query = query.Where(q => q.Status == status);
            }

            //ҵ��ԱƬ������
            if (salesmanOnlySeeHisCustomer)
            {
                //��ǰ�û����ǹ���Ա
                if (!currentUser.IsAdmin())
                {
                    List<int> districtIds = UserDistrictsRepository.Table.Where(ud => ud.UserId == currentUser.Id).Select(ud => ud.DistrictId).Distinct().ToList();
                    var prentDistricts = DistrictsRepository.Table.Where(w => districtIds.Contains(w.Id)).Select(s => s.ParentId).Distinct().ToList();
                    districtIds.AddRange(prentDistricts);
                    if (!districtIds.Contains(0)) districtIds.Add(0);
                    //��ǰ�û���Ƭ�� ������� �ն� ���ڵ�Ƭ��
                    query = query.Where(q => districtIds.Contains(q.DistrictId) || (q.CreatedUserId == currentUser.Id && q.DistrictId == 0));
                }
                //�����ǰԱ�����ǹ���Ա
                //bool isAdmin = false;
                //if (currentUser != null && currentUser.UserRoles != null && currentUser.UserRoles.Where(u => u.SystemName == DCMSDefaults.Administrators).Count() > 0)
                //{
                //    isAdmin = true;
                //}
                ////���ǹ���Ա
                //if (isAdmin == false)
                //{
                //    //��ǰ�û���Ƭ��
                //    List<int> districtIds = UserDistrictsRepository.Table.Where(ud => ud.UserId == currentUser.Id).Select(ud => ud.DistrictId).Distinct().ToList();
                //    //��ǰ�û���Ƭ�� ������� �ն� ���ڵ�Ƭ��
                //    query = query.Where(q => districtIds.Contains(q.DistrictId));
                //}
            }

            //var totalCount = query.Count();
            //var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            return new PagedList<Terminal>(query, pageIndex, pageSize);
        }



        public virtual IPagedList<Terminal> GetTerminals(int? storeId, int[] ids, string searchStr = null, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            if (pageSize >= 50)
                pageSize = 50;


            var query = from p in TerminalsRepository.Table
                        orderby p.Id descending
                        where !p.Deleted && p.Status && p.StoreId == storeId.Value
                        select p;

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0 && !t.Deleted
            //             select t).OrderByDescending(s => s.Id).AsQueryable();

            if (ids != null)
            {
                query = query.Where(p => !ids.Contains(p.Id));
            }

            if (!string.IsNullOrWhiteSpace(searchStr))
            {
                query = query.Where(c => c.Name.Contains(searchStr) || c.BossName.Contains(searchStr) || c.BossCall.Contains(searchStr));
            }

            query = query.OrderByDescending(c => c.CreatedOnUtc);
            //return new PagedList<Terminal>(query.ToList(), pageIndex, pageSize);
            //��ҳ��
            var totalCount = query.Count();
            var plists = query.Skip(pageIndex * pageSize).Take(pageSize).ToList();
            return new PagedList<Terminal>(plists, pageIndex, pageSize, totalCount);
        }


        public virtual IList<IGrouping<DateTime, Terminal>> GetTerminalsAnalysisByCreate(int? storeId, int? user, DateTime date)
        {
            if (user.HasValue && user != 0)
            {
                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == storeId && tr.TerminalId > 0 && !t.Deleted && t.CreatedUserId == user && t.CreatedOnUtc >= date
                //             select t).OrderByDescending(s => s.Id).AsQueryable();

                var query = from p in TerminalsRepository.Table
                            where p.StoreId == storeId && p.CreatedUserId == user && p.CreatedOnUtc >= date
                            select p;

                var result = query.AsEnumerable().GroupBy(t => t.CreatedOnUtc).OrderBy(g => g.Key);

                return result.ToList();
            }
            else
            {

                var query = from p in TerminalsRepository.Table
                            where p.StoreId == storeId && p.CreatedOnUtc >= date
                            select p;

                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == storeId && tr.TerminalId > 0 && !t.Deleted  && t.CreatedOnUtc >= date
                //             select t).OrderByDescending(s => s.Id).AsQueryable();

                var result = query.AsEnumerable().GroupBy(t => t.CreatedOnUtc).OrderBy(g => g.Key);

                return result.ToList();
            }
        }


        /// <summary>
        /// ����Id��ȡ�ն���Ϣ
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual Terminal GetTerminalById(int? store, int id)
        {
            if (id == 0)
                return null;

            var terminal = TerminalsRepository.ToCachedGetById(id);

            if (terminal != null && (store ?? 0) > 0 && terminal.StoreId != store)
                terminal.StoreId = store ?? 0;

            return terminal;
        }

        public virtual bool CheckTerminal(int? store, string name)
        {
            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == store && tr.TerminalId > 0 && !t.Deleted
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            //return query.Where(s => s.Name.Equals(name)).Count() > 0;

            return TerminalsRepository.Table
                .Where(a => a.StoreId == store && a.Name.Equals(name))
                .Count() > 0;
        }

        public virtual string GetTerminalName(int? store, int id)
        {
            if (id == 0)
            {
                return "";
            }
            var key = DCMSDefaults.TERMINAL_NAME_BY_ID_KEY.FillCacheKey(store ?? 0, id);
            return _cacheManager.Get(key, () =>
            {
                return TerminalsRepository.Table.FirstOrDefault(a => a.Id == id && a.StoreId == store)?.Name;
                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == store && tr.TerminalId > 0 
                //             select t).OrderByDescending(s => s.Id).AsQueryable();

                //return query.FirstOrDefault(a => a.Id == id)?.Name;
            });
        }

        public virtual string GetTerminalCode(int? store, int id)
        {
            if (id == 0)
            {
                return "";
            }

            var key = DCMSDefaults.TERMINAL_CODE_BY_ID_KEY.FillCacheKey(store ?? 0, id);
            return _cacheManager.Get(key, () =>
            {
                return TerminalsRepository.Table.Where(a => a.Id == id && a.StoreId == store).Select(a => a.Code).FirstOrDefault();
                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == store && tr.TerminalId > 0
                //             select t).OrderByDescending(s => s.Id).AsQueryable();
                //return query.Select(a => a.Code).FirstOrDefault();
            });
        }

        /// <summary>
        /// �����ն����ƻ�ȡ�ն�Ids
        /// </summary>
        /// <param name="terminalName"></param>
        /// <returns></returns>
        public virtual IList<int> GetTerminalIds(int? store, string terminalName, bool platform = false)
        {
            if (string.IsNullOrEmpty(terminalName))
            {
                return new List<int>();
            }
            var key = DCMSDefaults.TERMINAL_IDS_BY_NAME_KEY.FillCacheKey(store ?? 0, terminalName);
            return _cacheManager.Get(key, () =>
            {
                {
                    //var query = (from t in TerminalsRepository_RO.Table
                    //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                    //             where tr.StoreId == store && tr.TerminalId > 0
                    //             select t).OrderByDescending(s => s.Id).AsQueryable();
                    //return query.Where(a => a.Name.Contains(terminalName)).Select(a => a.Id).ToList();

                    return TerminalsRepository.Table.Where(a => a.Name.Contains(terminalName) && a.StoreId == store).Select(a => a.Id).ToList();
                }
            });

        }

        public virtual IList<int> GetDisTerminalIds(int? store, int districtId)
        {
            if (districtId == 0)
            {
                return new List<int>();
            }

            var ids = TerminalsRepository.Table.Where(s => s.StoreId == store && s.DistrictId == districtId).Select(a => a.Id).ToList();
            return ids;

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == store && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            //return query.Where(s => s.DistrictId == districtId).Select(a => a.Id).ToList();

        }

        /// <summary>
        /// �����ն˵ȼ�ID��ȡ�ն�Ids
        /// </summary>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public virtual IList<int> GetRankTerminalIds(int? store, int rankId)
        {
            if (rankId == 0)
            {
                return new List<int>();
            }

            var ids = TerminalsRepository.Table
                .Where(s => s.StoreId == store && s.RankId == rankId)
                .Select(a => a.Id)
                .ToList();

            return ids;

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == store && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            //return query.Where(s => s.RankId == rankId).Select(a => a.Id).ToList();
        }

        public virtual decimal GetTerminalMaxAmountOwed(int id)
        {
            if (id == 0)
            {
                return 0;
            }
            return TerminalsRepository.Table.Where(a => a.Id == id).Select(a => a.MaxAmountOwed).FirstOrDefault();
        }

        /// <summary>
        /// ��ȡһ���ն�ʵ��
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public virtual IList<Terminal> GetTerminalsByIds(int? store, int[] idArr, bool platform = false)
        {
            if (idArr == null || idArr.Length == 0)
            {
                return new List<Terminal>();
            }

            var key = DCMSDefaults.TERMINAL_BY_IDS_KEY.FillCacheKey(store ?? 0, string.Join("_", idArr.OrderBy(a => a)));
            return _cacheManager.Get(key, () =>
            {
                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == store && tr.TerminalId > 0
                //             select t).OrderByDescending(s => s.Id).AsQueryable();

                var query = from c in TerminalsRepository.TableNoTracking
                            where idArr.Contains(c.Id)
                            select c;

                if (platform)
                {
                    query = from c in TerminalsRepository_RO.TableNoTracking
                            where idArr.Contains(c.Id)
                            select c;
                }

                return query.Where(s => idArr.Contains(s.Id)).ToList();
            });

        }

        public virtual Dictionary<int, string> GetTerminalsDictsByIds(int storeId, int[] ids)
        {
            var categoryIds = new Dictionary<int, string>();
            if (ids.Count() > 0)
            {

                //var query = (from t in TerminalsRepository_RO.Table
                //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
                //             where tr.StoreId == storeId && tr.TerminalId > 0
                //             select t).OrderByDescending(s => s.Id).AsQueryable();
                //categoryIds = query.Select(s => new { s.Id, s.Name }).ToDictionary(k => k.Id, v => v.Name);

                categoryIds = UserRepository_RO.QueryFromSql<DictType>($"SELECT Id,Name as Name FROM dcms_crm.CRM_Terminals where StoreId = " + storeId + " and id in(" + string.Join(",", ids) + ");").ToDictionary(k => k.Id, v => v.Name);

            }
            return categoryIds;
        }

        /// <summary>
        /// ����Ƭ��Id��ȡƬ���������ն�
        /// </summary>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public virtual IList<Terminal> GetTerminalsByDistrictId(int storeId, int districtId)
        {
            if (districtId == 0)
            {
                return new List<Terminal>();
            }

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            //return query.Where(s=>s.DistrictId == districtId).ToList();

            var query = from c in TerminalsRepository.Table
                        where c.StoreId == storeId && c.DistrictId == districtId
                        select c;

            return query.ToList();

        }
        /// <summary>
        /// ��������Id��ȡ�����������ն�
        /// </summary>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public virtual IList<Terminal> GetTerminalsByChannelid(int storeId, int channelid)
        {
            if (channelid == 0)
            {
                return new List<Terminal>();
            }

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            // return query.Where(s => s.ChannelId == channelid).ToList();

            var query = from c in TerminalsRepository.Table
                        where c.StoreId == storeId && c.ChannelId == channelid
                        select c;
            return query.ToList();
        }

        /// <summary>
        /// ������·Id��ȡ��·�������ն�
        /// </summary>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public virtual IList<int> GetLineTierOptionLineids(int storeId, int lineid)
        {
            if (lineid == 0)
            {
                return null;
            }

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();

            var query = from c in LineTierOptionsRepository.Table
                        where c.StoreId == storeId && c.LineTierId == lineid
                        select c.Id;
            return query.ToList();

        }
        /// <summary>
        /// ����Ƭ��Id��ȡƬ���������ն�
        /// </summary>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public virtual IList<Terminal> GetTerminalsByDistrictId(int storeId, string key, int[] districtIds)
        {
            if (storeId == 0)
            {
                return new List<Terminal>();
            }

            var query = from c in TerminalsRepository.Table
                        where c.StoreId == storeId
                        select c;

            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();

            if (!string.IsNullOrEmpty(key))
            {
                query = query.Where(t => t.Name.Contains(key));
            }

            if (districtIds != null && districtIds.Length > 0)
            {
                query = query.Where(t => districtIds.Contains(t.DistrictId));
            }

            return query.ToList();
        }

        public virtual IList<Terminal> GetTerminalsByLineId(int storeId, int lineId)
        {
            try
            {
                if (lineId == 0)
                {
                    return new List<Terminal>();
                }

                var query_line = from l in LineTierOptionsRepository_RO.Table
                                 where l.StoreId == storeId && l.LineTierId == lineId
                                 select l.TerminalId;

                var query = from c in TerminalsRepository_RO.Table
                            where c.StoreId == storeId && query_line.ToList().Contains(c.Id)
                            select c;
                return query.ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }


            //var query = (from t in TerminalsRepository_RO.Table
            //             join tr in CRM_RELATIONRepository_RO.Table on t.Id equals tr.TerminalId
            //             where tr.StoreId == storeId && tr.TerminalId > 0
            //             select t).OrderByDescending(s => s.Id).AsQueryable();
            ///return query.Where(s=> s.LineId == lineId).ToList();
        }


        /// <summary>
        /// ����ն���Ϣ
        /// </summary>
        /// <param name="terminal"></param>
        public virtual void InsertTerminal(Terminal terminal, string storeCode)
        {
            if (terminal == null)
            {
                throw new ArgumentNullException("terminal");
            }

            var uow = TerminalsRepository.UnitOfWork;

            TerminalsRepository.Insert(terminal);

            //���ӳ��
            var relation = new CRM_RELATION()
            {
                TerminalId = terminal.Id,
                StoreId = terminal.StoreId,
                CreatedOnUtc = DateTime.Now,
                //DCMS�����ն˱������(�����̱���+����ID)
                PARTNER1 = $"{storeCode}{terminal.Id}",
                //�����̱���
                PARTNER2 = storeCode,
                RELTYP = "",
                ZUPDMODE = "",
                ZDATE = DateTime.Now,
            };
            CRM_RELATIONRepository.Insert(relation);


            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityInserted(terminal);
        }



        public virtual void InsertNewTerminal(NewTerminal terminal)
        {
            if (terminal == null)
            {
                throw new ArgumentNullException("terminal");
            }

            var uow = NewTerminalRepository.UnitOfWork;
            NewTerminalRepository.Insert(terminal);
            uow.SaveChanges();
        }


        public virtual void InsertRelation(CRM_RELATION relation)
        {
            if (relation == null)
            {
                throw new ArgumentNullException("relation");
            }
            var uow = TerminalsRepository.UnitOfWork;
            CRM_RELATIONRepository.Insert(relation);
            uow.SaveChanges();
            //֪ͨ
            _eventPublisher.EntityInserted(relation);
        }

        /// <summary>
        /// ɾ���ն���Ϣ
        /// </summary>
        /// <param name="terminal"></param>
        public virtual void DeleteTerminal(Terminal terminal)
        {
            if (terminal == null)
            {
                throw new ArgumentNullException("terminal");
            }

            var uow = TerminalsRepository.UnitOfWork;

            TerminalsRepository.Delete(terminal);

            //ɾ����ϵ
            var rels = CRM_RELATIONRepository.Table.Where(s => s.TerminalId == terminal.Id).Select(s => s).ToList();
            CRM_RELATIONRepository.Delete(rels);

            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityDeleted(terminal);
        }

        public virtual void UpdateTerminal(Terminal terminal)
        {
            if (terminal == null)
            {
                throw new ArgumentNullException("terminal");
            }

            var uow = TerminalsRepository.UnitOfWork;
            TerminalsRepository.Update(terminal);
            uow.SaveChanges();

            //֪ͨ
            _eventPublisher.EntityUpdated(terminal);

        }

        public virtual void UpdateTerminals(List<Terminal> terminals)
        {
            if (!terminals.Any())
            {
                throw new ArgumentNullException("terminals");
            }

            var uow = TerminalsRepository.UnitOfWork;
            TerminalsRepository.Update(terminals);
            uow.SaveChanges();

            //֪ͨ
            terminals.ForEach(t => {
                _eventPublisher.EntityUpdated(t);
            });
        }


        /// <summary>
        /// ����ն˿ͻ��Ƿ��������(�����)
        /// </summary>
        /// <param name="storeId"></param>
        /// <param name="terminalId"></param>
        /// <returns></returns>
        public bool CheckTerminalHasCampaignGives(int storeId, int terminalId)
        {
            //MYSQL
            string sqlString = $"with tmp(a) as (select t.id from dcms_crm.CRM_Terminals as t inner join  Campaign_Channel_Mapping as cp on t.ChannelId= cp.ChannelId inner join  Campaigns  as ca on cp.CampaignId = ca.Id inner join CampaignBuyProducts as buy on ca.Id=buy.CampaignId inner join CampaignGiveProducts as give on ca.Id = give.CampaignId  where  t.StoreId={storeId} and t.Id={terminalId}  AND (ca.StartTime <= curdate() and curdate() <=  ca.EndTime) and ca.Enabled = true and buy.Quantity>0 and give.Quantity>0 group by t.Id) select count(*) as 'Value' from tmp";

            var count = DistrictsRepository.QueryFromSql<IntQueryType>(sqlString).ToList().FirstOrDefault().Value;
            return count > 0;
        }

        /// <summary>
        /// ����ն˿ͻ��Ƿ��������(���ú�ͬ)
        /// </summary>
        /// <param name="stroeId"></param>
        /// <param name="terminalId"></param>
        /// <param name="businessUserId"></param>
        /// <returns></returns>
        public bool CheckTerminalHasCostContractGives(int storeId, int terminalId, int businessUserId)
        {
            //MYSQL
            //string sqlString = $"select count(0) as 'Value' from CostContractBills a inner join CostContractItems b on a.Id = b.CostContractBillId where a.StoreId = '{storeId}' and a.CustomerId = '{terminalId}' and a.EmployeeId = '{businessUserId}' and b.Total_Balance > 0";

            //������ҵ��Ա��ֻ�����ն�
            string sqlString = $"select count(0) as 'Value' from CostContractBills a inner join CostContractItems b on a.Id = b.CostContractBillId where a.StoreId = '{storeId}' and a.CustomerId = '{terminalId}' and b.Total_Balance > 0";

            var count = DistrictsRepository.QueryFromSql<IntQueryType>(sqlString).ToList().FirstOrDefault().Value;
            return count > 0;
        }

        /// <summary>
        /// ���ҵ�����״̬
        /// </summary>
        /// <param name="terminalId"></param>
        /// <returns></returns>
        public virtual bool CheckRelated(int terminalId)
        {
            try
            {
                string sqlString = @"select count(t.Id) from `dcms_crm`.`CRM_Terminals` as t left join VisitStore as vs on t.id = vs.TerminalId
                            left join  SaleReservationBills as vs1 on t.id = vs1.TerminalId
                            left join  SaleBills as vs2 on t.id = vs2.TerminalId
                            left join  ReturnReservationBills as vs3 on t.id = vs3.TerminalId
                            left join  ReturnBills as vs4 on t.id = vs4.TerminalId
                            left join  Receivables as vs5 on t.id = vs5.TerminalId
                            left join  LineTierOptions as vs6 on t.id = vs6.TerminalId
                            left join  InventoryReportSummaries as vs7 on t.id = vs7.TerminalId
                            left join  GiveQuotaRecords as vs8 on t.id = vs8.TerminalId
                            left join  DeliverySigns  as vs9 on t.id = vs9.TerminalId
                            left join  AdvanceReceiptBills as vs10 on t.id = vs10.CustomerId
                            where t.id = " + terminalId + "";

                return TerminalsRepository.QueryFromSql<IntQueryType>(sqlString).ToList().FirstOrDefault()?.Value > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }



        public virtual decimal GetMaxAmountOwed(int storeId, int terminalId)
        {
            var query = from c in TerminalsRepository.Table
                        where c.StoreId == storeId && c.Id == terminalId
                        select c.MaxAmountOwed;
            return query.FirstOrDefault();
        }


        public virtual IList<Terminal> GetTerminalsByKeyWord(int storeId, string keyWord)
        {
            try
            {
                if (storeId == 0)
                {
                    return new List<Terminal>();
                }

                var query = from c in TerminalsRepository.Table
                            where c.StoreId == storeId
                            select c;

                if (!string.IsNullOrEmpty(keyWord))
                {
                    query = query.Where(t => t.Name.Contains(keyWord) || t.Code.Contains(keyWord) || t.BossCall.Contains(keyWord));
                }

                return query.ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Terminal FindTerminalById(int? store, int id)
        {
            try
            {
                if (id == 0)
                    return null;

                var terminal = TerminalsRepository.Find(id);

                if (terminal != null && (store ?? 0) > 0 && terminal.StoreId != store)
                    terminal.StoreId = store ?? 0;

                return terminal;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IList<Terminal> GetTerminalsByDistrictIds(int? store, IList<int> districtIds, int lineId = 0)
        {
            try
            {
                var query = from t in TerminalsRepository_RO.Table
                            where t.StoreId.Equals(store)
                            select t;
                if (districtIds?.Count > 0)
                {
                    query = query.Where(w => districtIds.Contains(w.DistrictId));
                }
                if (lineId > 0)
                {
                    var query_line = from l in LineTierOptionsRepository_RO.Table
                                     where l.StoreId.Equals(store) && l.LineTierId.Equals(lineId)
                                     select l.TerminalId;
                    query = query.Where(w => query_line.ToList().Contains(w.Id));
                }
                return query.ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
