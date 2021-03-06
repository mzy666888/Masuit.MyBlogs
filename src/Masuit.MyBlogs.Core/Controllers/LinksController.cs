﻿using Masuit.MyBlogs.Core.Common;
using Masuit.MyBlogs.Core.Extensions;
using Masuit.MyBlogs.Core.Models.DTO;
using Masuit.MyBlogs.Core.Models.Entity;
using Masuit.MyBlogs.Core.Models.Enum;
using Masuit.Tools;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Masuit.MyBlogs.Core.Controllers
{
    /// <summary>
    /// 友情链接管理
    /// </summary>
    public class LinksController : BaseController
    {
        public IHttpClientFactory HttpClientFactory { get; set; }
        private HttpClient HttpClient => HttpClientFactory.CreateClient();

        /// <summary>
        /// 友情链接页
        /// </summary>
        /// <returns></returns>
        [Route("links"), ResponseCache(Duration = 600, VaryByHeader = "Cookie")]
        public ActionResult Index()
        {
            var list = LinksService.GetQueryFromCache<bool, LinksOutputDto>(l => l.Status == Status.Available, l => l.Recommend, false).ToList();
            return CurrentUser.IsAdmin ? View("Index_Admin", list) : View(list);
        }

        /// <summary>
        /// 申请友链
        /// </summary>
        /// <param name="links"></param>
        /// <returns></returns>
        public async Task<ActionResult> Apply(Links links)
        {
            if (!links.Url.MatchUrl())
            {
                return ResultData(null, false, "添加失败！链接非法！");
            }

            var host = new Uri(links.Url).Host;
            if (LinksService.Any(l => l.Url.Contains(host)))
            {
                return ResultData(null, false, "添加失败！检测到您的网站已经是本站的友情链接了！");
            }

            HttpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
            HttpClient.DefaultRequestHeaders.Referrer = new Uri(Request.Scheme + "://" + Request.Host.ToString());
            return await await HttpClient.GetAsync(links.Url).ContinueWith(async t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    return ResultData(null, false, "添加失败！检测到您的网站疑似挂了，或者连接到你网站的时候超时，请检查下！");
                }

                var res = await t;
                if (!res.IsSuccessStatusCode)
                {
                    return ResultData(null, false, "添加失败！检测到您的网站疑似挂了！返回状态码为：" + res.StatusCode);
                }

                var s = await res.Content.ReadAsStringAsync();
                if (!s.Contains(CommonHelper.SystemSettings["Domain"]))
                {
                    return ResultData(null, false, $"添加失败！检测到您的网站上未将本站设置成友情链接，请先将本站主域名：{CommonHelper.SystemSettings["Domain"]}在您的网站设置为友情链接，并且能够展示后，再次尝试添加即可！");
                }

                var entry = LinksService.Get(l => l.Url.Equals(links.Url));
                bool b;
                if (entry is null)
                {
                    b = LinksService.AddEntitySaved(links) != null;
                }
                else
                {
                    entry.Url = links.Url;
                    entry.Except = links.Except;
                    entry.Name = links.Name;
                    entry.Recommend = links.Recommend;
                    b = LinksService.SaveChanges() > 0;
                }

                return ResultData(null, b, b ? "添加成功！这可能有一定的延迟，如果没有看到您的链接，请稍等几分钟后刷新页面即可，如有疑问，请联系站长。" : "添加失败！这可能是由于网站服务器内部发生了错误，如有疑问，请联系站长。");

            });
        }

        /// <summary>
        /// 添加友链
        /// </summary>
        /// <param name="links"></param>
        /// <returns></returns>
        [MyAuthorize]
        public ActionResult Add(Links links)
        {
            var entry = LinksService.GetById(links.Id);
            bool b;
            if (entry is null)
            {
                b = LinksService.AddEntitySaved(links) != null;
            }
            else
            {
                entry.Url = links.Url;
                entry.Except = links.Except;
                entry.Name = links.Name;
                entry.Recommend = links.Recommend;
                b = LinksService.SaveChanges() > 0;
            }

            return b ? ResultData(null, message: "添加成功！") : ResultData(null, false, "添加失败！");
        }

        /// <summary>
        /// 检测回链
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        [MyAuthorize]
        public async Task<ActionResult> Check(string link)
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
            return await await HttpClient.GetAsync(link).ContinueWith(async t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    return ResultData(null, false, link + " 似乎挂了！");
                }

                var res = await t;
                if (!res.IsSuccessStatusCode)
                {
                    return ResultData(null, false, link + " 对方网站返回错误的状态码！http响应码为：" + res.StatusCode);
                }

                var s = await res.Content.ReadAsStringAsync();
                if (s.Contains(CommonHelper.SystemSettings["Domain"]))
                {
                    return ResultData(null, true, "友情链接正常！");
                }

                return ResultData(null, false, link + " 对方似乎没有本站的友情链接！");

            });

        }

        /// <summary>
        /// 删除友链
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [MyAuthorize]
        public ActionResult Delete(int id)
        {
            bool b = LinksService.DeleteByIdSaved(id);
            return ResultData(null, b, b ? "删除成功！" : "删除失败！");
        }

        /// <summary>
        /// 编辑友链
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [MyAuthorize]
        public ActionResult Edit(Links model)
        {
            Links links = LinksService.GetById(model.Id);
            links.Name = model.Name;
            links.Url = model.Url;
            bool b = LinksService.SaveChanges() > 0;
            return ResultData(null, b, b ? "保存成功" : "保存失败");
        }

        /// <summary>
        /// 所有的友情链接
        /// </summary>
        /// <returns></returns>
        [MyAuthorize]
        public ActionResult Get()
        {
            List<Links> list = LinksService.GetAll().OrderBy(p => p.Status).ThenByDescending(p => p.Recommend).ThenByDescending(p => p.Id).ToList();
            return ResultData(list);
        }

        /// <summary>
        /// 切换友情链接的白名单状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ToggleWhitelist(int id, bool state)
        {
            Links link = LinksService.GetById(id);
            link.Except = !state;
            bool b = LinksService.SaveChanges() > 0;
            return ResultData(null, b, b ? "切换成功！" : "切换失败！");
        }

        /// <summary>
        /// 切换友情链接的推荐状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ToggleRecommend(int id, bool state)
        {
            Links link = LinksService.GetById(id);
            link.Recommend = !state;
            bool b = LinksService.SaveChanges() > 0;
            return ResultData(null, b, b ? "切换成功！" : "切换失败！");
        }

        /// <summary>
        /// 切换友情链接可用状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Toggle(int id, bool state)
        {
            Links link = LinksService.GetById(id);
            link.Status = !state ? Status.Available : Status.Unavailable;
            bool b = LinksService.SaveChanges() > 0;
            return ResultData(null, b, b ? "切换成功！" : "切换失败！");
        }
    }
}