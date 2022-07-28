using DCMS.Core.Domain.Tasks;
using System.Collections.Generic;

namespace DCMS.Services.Tasks
{
    /// <summary>
    /// ����ʵ������ӿ�
    /// </summary>
    public partial interface IScheduleTaskService
    {

        void DeleteTask(ScheduleTask task);


        ScheduleTask GetTaskById(int taskId);


        ScheduleTask GetTaskByType(string type);


        IList<ScheduleTask> GetAllTasks(bool showHidden = false);

        void InsertTask(ScheduleTask task);


        void UpdateTask(ScheduleTask task);
    }
}
