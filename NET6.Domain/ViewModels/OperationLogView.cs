﻿namespace NET6.Domain.ViewModels
{
    /// <summary>
    /// 操作日志
    /// </summary>
    public class OperationLogView
    {
        /// <summary>
        /// 编号
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// IP
        /// </summary>
        public string IP { get; set; }
        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }
        /// <summary>
        /// 操作系统
        /// </summary>
        public string Os { get; set; }
        /// <summary>
        /// 设备
        /// </summary>
        public string Device { get; set; }
        /// <summary>
        /// 浏览器信息
        /// </summary>
        public string BrowserInfo { get; set; }
        /// <summary>
        /// 耗时（毫秒）
        /// </summary>
        public long ElapsedMilliseconds { get; set; }
        /// <summary>
        /// 接口名称
        /// </summary>
        public string ApiLabel { get; set; }
        /// <summary>
        /// 接口地址
        /// </summary>
        public string ApiPath { get; set; }
        /// <summary>
        /// 接口提交方法
        /// </summary>
        public string ApiMethod { get; set; }
        /// <summary>
        /// 操作参数
        /// </summary>
        public string Params { get; set; }
        /// <summary>
        /// 操作结果
        /// </summary>
        public string Result { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
    }
}
