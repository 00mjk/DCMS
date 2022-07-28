using DCMS.Core;
using DCMS.Data.Extensions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace DCMS.Web.Framework.Validators
{
    /// <summary>
    /// FluentValidation ��֤����
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
    public abstract class BaseDCMSValidator<TModel> : AbstractValidator<TModel> where TModel : class
    {

        protected BaseDCMSValidator()
        {
            PostInitialize();
        }

        /// <summary>
        /// �������Զ��岿��������д�˷������Ա����캯�����һЩ�Զ����ʼ�����롣
        /// </summary>
        protected virtual void PostInitialize()
        {
        }

        /// <summary> 
        /// ���ʵ���Ϊ���ݿ�ģ��������֤����
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        /// <param name="filterStringPropertyNames">Properties to skip</param>
        protected virtual void SetDatabaseValidationRules<TEntity>(DbContext dbContext, params string[] filterStringPropertyNames)
            where TEntity : BaseEntity
        {
            SetStringPropertiesMaxLength<TEntity>(dbContext, filterStringPropertyNames);
            SetDecimalMaxValue<TEntity>(dbContext);
        }

        /// <summary>
        /// �ʵ���Ϊ���ݿ�ģ���ַ����������ó�����֤����
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        /// <param name="filterPropertyNames">Properties to skip</param>
        protected virtual void SetStringPropertiesMaxLength<TEntity>(DbContext dbContext, params string[] filterPropertyNames)
            where TEntity : BaseEntity
        {
            if (dbContext == null)
            {
                return;
            }

            //filter model properties for which need to get max lengths
            var modelPropertyNames = typeof(TModel).GetProperties()
                .Where(property => property.PropertyType == typeof(string) && !filterPropertyNames.Contains(property.Name))
                .Select(property => property.Name).ToList();

            //get max length of these properties
            var propertyMaxLengths = dbContext.GetColumnsMaxLength<TEntity>()
                .Where(property => modelPropertyNames.Contains(property.Name) && property.MaxLength.HasValue);

            //create expressions for the validation rules
            var maxLengthExpressions = propertyMaxLengths.Select(property => new
            {
                MaxLength = property.MaxLength.Value,
                Expression = DynamicExpressionParser.ParseLambda<TModel, string>(null, false, property.Name)
            }).ToList();

            //define string length validation rules
            foreach (var expression in maxLengthExpressions)
            {
                RuleFor(expression.Expression).Length(0, expression.MaxLength);
            }
        }

        /// <summary>
        /// �ʵ���Ϊ���ݿ�ģ�͸��������������ֵ��֤����
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        protected virtual void SetDecimalMaxValue<TEntity>(DbContext dbContext) where TEntity : BaseEntity
        {
            if (dbContext == null)
            {
                return;
            }

            //filter model properties for which need to get max values
            var modelPropertyNames = typeof(TModel).GetProperties()
                .Where(property => property.PropertyType == typeof(decimal))
                .Select(property => property.Name).ToList();

            //get max values of these properties
            var decimalPropertyMaxValues = dbContext.GetDecimalColumnsMaxValue<TEntity>()
                .Where(property => modelPropertyNames.Contains(property.Name) && property.MaxValue.HasValue);

            //create expressions for the validation rules
            var maxValueExpressions = decimalPropertyMaxValues.Select(property => new
            {
                MaxValue = property.MaxValue.Value,
                Expression = DynamicExpressionParser.ParseLambda<TModel, decimal>(null, false, property.Name)
            }).ToList();

            //define decimal validation rules
            foreach (var expression in maxValueExpressions)
            {
                RuleFor(expression.Expression).IsDecimal(expression.MaxValue)
                    .WithMessage(string.Format("�������Χ��{0}", expression.MaxValue - 1));
            }
        }
    }
}