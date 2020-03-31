using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Countersoft.Gemini.Infrastructure.Api;
using Countersoft.Foundation.Commons.Extensions;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Controllers.Api;
using Countersoft.Gemini.Extensibility.Apps;
using System.Web.Routing;
using System.Web.Http;
using Countersoft.Gemini.Infrastructure.Helpers;
using Countersoft.Gemini;
using Countersoft.Gemini.Infrastructure.Managers;
using System.Collections;

namespace GeckoboardConnector
{
    public class GeckoboardRoutes : IAppRoutes
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapHttpRoute(null, "api/wallboard/{cardId}/{boardoption}/{limit}/{restrictTo}/{onlySlaItems}", new { controller = "Geckoboard", action = "Get", limit = RouteParameter.Optional, restrictTo = RouteParameter.Optional, onlySlaItems = RouteParameter.Optional }, new { httpMethod = new HttpMethodConstraint(new string[] { "GET" }) });
        }
    }

    [OutputCache(Duration = 0, NoStore = true, Location = System.Web.UI.OutputCacheLocation.None)]
    public class GeckoboardController : BaseApiController
    {
        [System.Web.Mvc.HttpGet]
        public object Get(int cardId, string boardOption, int limit = 0, string restrictTo = "", int onlySlaItems = 0)
        {
            object result = string.Empty;
            
            string username = string.Empty;

            bool onlyIncludeSlaItems = onlySlaItems == 1 ? true : false;

            if (Request.Headers.Authorization != null && Request.Headers.Authorization.Parameter != null)
            {
                username = Encoding.Default.GetString(Convert.FromBase64String(Request.Headers.Authorization.Parameter));
            }

            if (username.Length == 0 || GeminiApp.Config.ApiKey.Length == 0 || !username.StartsWith(GeminiApp.Config.ApiKey, StringComparison.InvariantCultureIgnoreCase))
            {
                if (GeminiApp.Config.ApiKey.IsEmpty())
                {
                    result = "Web.config is missing API key";
                }
                else
                {
                    result = "Wrong API key: " + username;
                }

                return result;
            }

            var card = NavigationCardsManager.Get(cardId);

            if (card == null)
            {
                result = "Card Not Found: " + cardId;
                
                return result;
            }

            if (card.UserId != null)
            {
                UserContext.User = UserManager.Get(card.UserId.Value);
                
                UserContext.PermissionsManager = UserContext.PermissionsManager.Copy(UserContext.User);
                
                PermissionsManager = UserContext.PermissionsManager;
            }

            //necessary to prevent updating the actual card filter
            var filter = new IssuesFilter(card.Filter);
            
            var issues = new List<IssueDto>();

            WallboardHelper.RAGNumberWidget rag = new WallboardHelper.RAGNumberWidget();
            
            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();
            
            WallboardHelper.FunnelWidget funnel = new WallboardHelper.FunnelWidget();

            var closed = 0;
            
            var x = 0;

            if (boardOption.HasValue()) boardOption = boardOption.ToLowerInvariant();

            restrictTo = restrictTo.ToLowerInvariant();

            var cacheKey = string.Concat(boardOption, '_', restrictTo, '_', cardId, '_', limit, '_', onlySlaItems);
            var cached = GeckoboardCache.Get(cacheKey);
            if(cached != null)
            {
                return cached;
            }

            switch (boardOption)
            {
                case "progress":
                    issues = IssueManager.GetFiltered(filter, true);

                    var total = issues.Count;
                    
                    closed = issues.Count(i => i.IsClosed);

                    var remaining = total - closed;

                    rag.item = new WallboardHelper.TextValue[3];
                    
                    rag.item[0] = new WallboardHelper.TextValue() { text = ResourceKeys.Total, value = total };
                    
                    rag.item[1] = new WallboardHelper.TextValue() { text = ResourceKeys.Open, value = remaining };
                    
                    rag.item[2] = new WallboardHelper.TextValue() { text = ResourceKeys.Closed, value = closed };
                    
                    result = rag;
                    
                    break;
                case "opened-count":
                    var open = IssueManager.GetFiltered(filter, true).Count(i => !i.IsClosed);

                    textW.item = new WallboardHelper.TextType[1];
                    
                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='opened-count'>{0}</div>", open.ToString()), type = 0 };
                    
                    result = textW;
                    
                    break;
                case "closed-count":
                    closed = IssueManager.GetFiltered(filter, true).Count(i => i.IsClosed);
                    
                    textW.item = new WallboardHelper.TextType[1];
                    
                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='closed-count'>{0}</div>", closed.ToString()), type = 0 };
                    
                    result = textW;
                    
                    break;
                case "breached-count":
                    filter.SLAStatus = Constants.SLAStatusBreach.ToString();
                    var breached = IssueManager.GetFiltered(filter, true).Count;
                    
                    textW.item = new WallboardHelper.TextType[1];

                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='breached-count count'>{0}</div>", breached.ToString()), type = 0 };
                    
                    result = textW;
                    break;
                case "red-count":
                    filter.SLAStatus = Constants.SLAStatusRed.ToString();
                    var red = IssueManager.GetFiltered(filter, true).Count;

                    textW.item = new WallboardHelper.TextType[1];

                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='red-count count'>{0}</div>", red.ToString()), type = 0 };

                    result = textW;
                    break;
                case "amber-count":
                    filter.SLAStatus = Constants.SLAStatusAmber.ToString();
                    var amber = IssueManager.GetFiltered(filter, true).Count;

                    textW.item = new WallboardHelper.TextType[1];

                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='amber-count count'>{0}</div>", amber.ToString()), type = 0 };

                    result = textW;
                    break;
                case "green-count":
                    //filter.SLAStatus = Constants.SLAStatusGreen.ToString();
                    filter.SLAItems = true;
                    var allItems = IssueManager.GetFiltered(filter, true);
                    int count = 0;

                    if (allItems.Count > 0)
                    {
                        count = allItems.FindAll(s => s.Entity.SLAId.HasValue && s.Entity.SLAStatus == null || (s.Entity.SLAStatus.HasValue && s.Entity.SLAStatus.Value == Constants.SLAStatusGreen)).Count;
                    }

                    textW.item = new WallboardHelper.TextType[1];

                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='green-count count'>{0}</div>", count.ToString()), type = 0 };

                    result = textW;
                    break;
                case "all":
                case "opened":
                case "closed":
                case "due-today":
                case "due-tomorrow":
                case "due-thisweek":
                case "due-nextweek":
                case "opened-recently":
                case "closed-recently":
                case "breached-total-list":
                    result = items(filter, limit, boardOption);
                    break;
                case "workload":
                    Dictionary<int, Pair<string, int>> resourcesGroup = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);

                    foreach (var issue in issues)
                    {
                        foreach (var resource in issue.Resources)
                        {
                            if (!resourcesGroup.ContainsKey(resource.Entity.Id))
                            {
                                resourcesGroup.Add(resource.Entity.Id, new Pair<string, int>(resource.Fullname, 0));
                            }

                            resourcesGroup[resource.Entity.Id].Value++;
                        }
                    }

                    var workload = resourcesGroup.OrderByDescending(r => r.Value.Value);
                    
                    StringBuilder wBuffer = new StringBuilder("<table class='workload-list'>");
                    
                    foreach (var key in workload)
                    {
                        wBuffer.Append(string.Format("<tr><td>{0}</td><td>{1}</td></tr>", key.Value.Key, key.Value.Value));
                    }
                    
                    wBuffer.Append("</table>");
                    
                    textW.item = new WallboardHelper.TextType[1];
                    
                    textW.item[0] = new WallboardHelper.TextType() { text = wBuffer.ToString(), type = 0 };
                    
                    result = textW;
                    
                    break;
                case "workload-pie":
                    Dictionary<int, Pair<string, int>> resourcesGroupPie = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        foreach (var resource in issue.Resources)
                        {
                            if (!resourcesGroupPie.ContainsKey(resource.Entity.Id))
                            {
                                resourcesGroupPie.Add(resource.Entity.Id, new Pair<string, int>(resource.Fullname, 0));
                            }
                            resourcesGroupPie[resource.Entity.Id].Value++;
                        }
                    }

                    var workloadPie = resourcesGroupPie.OrderByDescending(r => r.Value.Value);

                    var dataList = new List<List<object>>();
                    
                    foreach (var key in workloadPie)
                    {
                        dataList.Add(new List<object>() { key.Value.Key, key.Value.Value });
                    }

                    result = pieChart(dataList);
                    
                    break;
                case "progress-pie":
                    issues = IssueManager.GetFiltered(filter, true);
                    
                    var totalProgressPie = issues.Count;
                    
                    var closedProgressPie = issues.Count(i => i.IsClosed);
                    
                    var remainingProgressPie = totalProgressPie - closedProgressPie;
                    
                    var dataListProgressPie = new List<List<object>>();

                    dataListProgressPie.Add(new List<object>() { ResourceKeys.Open, totalProgressPie });
                    
                    dataListProgressPie.Add(new List<object>() { ResourceKeys.Remaining, remainingProgressPie });
                    
                    dataListProgressPie.Add(new List<object>() { ResourceKeys.Closed, closedProgressPie });
                    
                    result = pieChart(dataListProgressPie);
                    
                    break;
                case "workload-funnel":
                    Dictionary<int, Pair<string, int>> resourcesGroupFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        foreach (var resource in issue.Resources)
                        {
                            if (!resourcesGroupFunnel.ContainsKey(resource.Entity.Id))
                            {
                                resourcesGroupFunnel.Add(resource.Entity.Id, new Pair<string, int>(resource.Fullname, 0));
                            }
                            resourcesGroupFunnel[resource.Entity.Id].Value++;
                        }
                    }

                    var workloadFunnel = resourcesGroupFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[workloadFunnel.Count()];

                    foreach (var key in workloadFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "type-funnel":
                    Dictionary<int, Pair<string, int>> resourcesIssueTypeFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        if (!resourcesIssueTypeFunnel.ContainsKey(issue.Entity.TypeId))
                        {
                            resourcesIssueTypeFunnel.Add(issue.Entity.TypeId, new Pair<string, int>(issue.Type, 0));
                        }
                        resourcesIssueTypeFunnel[issue.Entity.TypeId].Value++;
                    }

                    var IssueTypesFunnel = resourcesIssueTypeFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[IssueTypesFunnel.Count()];

                    foreach (var key in IssueTypesFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "status-funnel":
                    Dictionary<int, Pair<string, int>> resourcesIssueStatusFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        if (!resourcesIssueStatusFunnel.ContainsKey(issue.Entity.StatusId))
                        {
                            resourcesIssueStatusFunnel.Add(issue.Entity.StatusId, new Pair<string, int>(issue.Status, 0));
                        }
                        resourcesIssueStatusFunnel[issue.Entity.StatusId].Value++;
                    }

                    var IssueStatusFunnel = resourcesIssueStatusFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[IssueStatusFunnel.Count()];

                    foreach (var key in IssueStatusFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "component-funnel":
                    Dictionary<int, Pair<string, int>> resourcesIssueComponentFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        foreach (var resource in issue.Components)
                        {
                            if (!resourcesIssueComponentFunnel.ContainsKey(resource.Entity.Id))
                            {
                                resourcesIssueComponentFunnel.Add(resource.Entity.Id, new Pair<string, int>(resource.Entity.Name, 0));
                            }
                            resourcesIssueComponentFunnel[resource.Entity.Id].Value++;
                        }
                    }

                    var IssueComponentFunnel = resourcesIssueComponentFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[IssueComponentFunnel.Count()];

                    foreach (var key in IssueComponentFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "priority-funnel":
                    Dictionary<int, Pair<string, int>> resourcesIssuePriorityFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        if (!resourcesIssuePriorityFunnel.ContainsKey(issue.Entity.PriorityId))
                        {
                            resourcesIssuePriorityFunnel.Add(issue.Entity.PriorityId, new Pair<string, int>(issue.Priority, 0));
                        }

                        resourcesIssuePriorityFunnel[issue.Entity.PriorityId].Value++;
                    }

                    var IssuePriorityFunnel = resourcesIssuePriorityFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[IssuePriorityFunnel.Count()];

                    foreach (var key in IssuePriorityFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "severity-funnel":
                    Dictionary<int, Pair<string, int>> resourcesIssueSeverityFunnel = new Dictionary<int, Pair<string, int>>();
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    foreach (var issue in issues)
                    {
                        if (!resourcesIssueSeverityFunnel.ContainsKey(issue.Entity.SeverityId))
                        {
                            resourcesIssueSeverityFunnel.Add(issue.Entity.SeverityId, new Pair<string, int>(issue.Severity, 0));
                        }

                        resourcesIssueSeverityFunnel[issue.Entity.SeverityId].Value++;
                    }

                    var IssueSeverityFunnel = resourcesIssueSeverityFunnel.OrderByDescending(r => r.Value.Value);
                    
                    funnel = new WallboardHelper.FunnelWidget();
                    
                    funnel.percentage = "hide";
                    
                    funnel.item = new WallboardHelper.ValueLabel[IssueSeverityFunnel.Count()];

                    foreach (var key in IssueSeverityFunnel)
                    {
                        funnel.item[x++] = new WallboardHelper.ValueLabel { label = key.Value.Key, value = key.Value.Value };
                    }
                    
                    result = funnel;
                    
                    break;
                case "open-sla":
                case "closed-sla":
                    result = getSlaCount(boardOption, filter, restrictTo);
                    break;
                case "average-sla-response":              
                case "average-sla-closing":
                    filter.OnlySLAItems = onlyIncludeSlaItems;
                    result = getSlaTime(boardOption, filter, restrictTo);                    
                    break;
                case "average-sla-response-total":
                case "average-sla-closing-total":
                    filter.OnlySLAItems = onlyIncludeSlaItems;
                    result = getSlaTimeTotal(boardOption, filter, restrictTo);
                    break;
                case "sla-status-breakdown-list":
                case "sla-status-breakdown-pie":
                    result = getSlaStatusBreakdown(boardOption, filter, restrictTo);
                    break;
                case "breached-sla-list":
                    result = getItemsPerSla(boardOption, filter, limit, restrictTo);
                    break;
                default:
                    result = "Unrecognized action specified: " + boardOption;
                    break;
            }

            GeckoboardCache.Set(cacheKey, result);
            return result;
        }

        public object getItemsPerSla(string boardOption, IssuesFilter cardFilter, int limit, string restrictTo)
        {
            SLAManager slaManager = new SLAManager(IssueManager);
            var allSlas = slaManager.GetAll();

            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();
            textW.item = new WallboardHelper.TextType[1];
            textW.item[0] = new WallboardHelper.TextType() { text = "", type = 0 };

            Dictionary<int, string> slaFormatted = new Dictionary<int, string>();

            foreach(var sla in allSlas)
            {
                slaFormatted.Add(sla.Id, sla.Name);
            }

            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);

            filter.OnlySLAItems = true;
            filter.SLAStatus = Constants.SLAStatusBreach.ToString();

            if (limit > 0)
            {
                filter.MaxItemsToReturn = limit;
            }

            var allIssues = IssueManager.GetFiltered(filter);

            if (allIssues.Count == 0) return textW;
            

            Dictionary<int, List<IssueDto>> result = new Dictionary<int, List<IssueDto>>();

            foreach (var issue in allIssues)
            {
                if (!issue.Entity.SLAId.HasValue) continue;

                if (result.ContainsKey(issue.Entity.SLAId.Value))
                {                    
                    result[issue.Entity.SLAId.Value].Add(issue);
                }
                else
                {
                    result.Add(issue.Entity.SLAId.Value, new List<IssueDto>() { issue });
                }
            }

            StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", boardOption));

            foreach (var item in result)
            {
                iBuffer.Append(string.Format("<tr class='sla-label'><td colspan='2'>{0}</td></tr>", slaFormatted[item.Key]));

                foreach (var issue in item.Value)
                {
                    iBuffer.Append(string.Format("<tr class='sla-value' title='{0}'><td>{1}</td><td title='{2}'>{3}</td></tr>", issue.IssueKey, issue.IssueKey.ToMaxMore(15), issue.Title, issue.Title.ToMaxMore(50)));
                }          
            }            

            iBuffer.Append("</table>");

            textW.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };

            return textW;
        }

        public object pieChart(List<List<object>> dataList, string boardOption = "default")
        {
            WallboardHelper.PieChartWidget pie = new WallboardHelper.PieChartWidget();
            
            pie.chart = new WallboardHelper.PieChartWidget.Chart();
            
            pie.chart.renderTo = "container";

            pie.chart.backgroundColor = "#2A2A2A";
            
            pie.chart.borderColor = "container";

            pie.chart.lineColor = "rgba(35,37,38,100)";

            pie.chart.plotBackgroundColor = "#2A2A2A";
            
            pie.chart.plotBorderColor = "rgba(35,37,38,100)";

            pie.tooltip = new WallboardHelper.PieChartWidget.ToolTip();
            
            pie.tooltip.formatter = "function() {  return '<b>' + this.point.name + '</b> ' + Math.round(this.percentage) + '%'; } ";

            pie.title = new WallboardHelper.PieChartWidget.Title();
            
            pie.title.text = string.Empty;

            pie.plotOptions = new WallboardHelper.PieChartWidget.PlotOption();
            
            pie.plotOptions.pie = new WallboardHelper.PieChartWidget.PlotOption.Pie();
            
            pie.plotOptions.pie.showInLegend = true;
            
            pie.plotOptions.pie.dataLabels = new WallboardHelper.PieChartWidget.PlotOption.Pie.DataLabels();

            pie.plotOptions.pie.dataLabels.enabled = true;
            
            pie.plotOptions.pie.dataLabels.formatter = "function() {  return this.y; } ";

            pie.series = new WallboardHelper.PieChartWidget.Series[1];
            
            pie.series[0] = new WallboardHelper.PieChartWidget.Series { name = "", type = "pie", data = dataList };

            if (boardOption == "sla-status-breakdown-pie")
            {
                pie.colors = new string[] { "#ffffff", "#D42F2F", "#E69C53", "#5A8756" };
                pie.plotOptions.pie.showInLegend = false;
                pie.plotOptions.pie.dataLabels.enabled = false;
            }

            return pie;
        }

        public object items(IssuesFilter cardFilter, int limit, string option)
        {
            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();

            IEnumerable<IssueDto> tmp_issues;
            
            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);

            var issues = new List<IssueDto>();

            if (option == "closed-recently")
            {
                if (filter.IncludeClosed)
                {
                    filter.SystemFilter = IssuesFilter.SystemFilterTypes.RecentlyClosedIssues;

                    issues = IssueManager.GetFiltered(filter, true);
                    
                    tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
                }
                else
                    tmp_issues = new List<IssueDto>();
            }
            else if (option == "opened-recently")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.RecentlyCreatedIssues;

                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-nextweek")
            {
                DateTime input = DateTime.Today;
                
                int delta = DayOfWeek.Monday - input.DayOfWeek;
                
                DateTime monday = input.AddDays(delta);

                filter.InitialDueDate = monday.AddDays(7).ToShortDateString();
                
                filter.FinalDueDate = monday.AddDays(13).ToShortDateString();

                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-thisweek")
            {
                DateTime input = DateTime.Today;
                
                int delta = DayOfWeek.Monday - input.DayOfWeek;
                
                DateTime monday = input.AddDays(delta);

                filter.InitialDueDate = monday.ToShortDateString();
                
                filter.FinalDueDate = monday.AddDays(7).ToShortDateString();

                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-tomorrow")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.DueTomorrowIssues;

                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-today")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.DueTodayIssues;

                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "closed")
            {
                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.FindAll(i => i.IsClosed).OrderByDescending(i => i.Created).Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "opened")
            {
                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.FindAll(i => !i.IsClosed).OrderByDescending(i => i.Created).Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "breached-total-list")
            {
                filter.OnlySLAItems = true;
                filter.SLAStatus = Constants.SLAStatusBreach.ToString();

                if (limit > 0)
                {
                    filter.MaxItemsToReturn = limit;
                }

                tmp_issues = IssueManager.GetFiltered(filter, true);
            }
            else
            {
                issues = IssueManager.GetFiltered(filter, true);
                
                tmp_issues = issues.OrderByDescending(i => i.Revised).Take((limit == 0 ? issues.Count : limit));
            }

            StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", option));

            if (tmp_issues.Count() > 0)
            {
                foreach (var issue in tmp_issues)
                {
                    iBuffer.Append(string.Format("<tr><td title='{0}'>{1}</td><td title='{2}'>{3}</td></tr>", issue.IssueKey, issue.IssueKey.ToMaxMore(15), issue.Title, issue.Title.ToMaxMore(50)));
                }
            }

            iBuffer.Append("</table>");
            
            textW.item = new WallboardHelper.TextType[1];
            
            textW.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };
            
            return textW;
        }

        public object getSlaStatusBreakdown(string boardOption, IssuesFilter cardFilter, string restrictTo)
        {
            List<int> closedStatuses = MetaManager.StatusGetClosed();

            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);
                        
            if (boardOption == "sla-status-breakdown-pie")
            {
                filter.IncludeClosed = true;
                
                var allIssues = GeminiContext.Issues.GetIssues(ItemFilterManager.TransformFilter(filter), 0, Cache.CustomFields.GetAll(), closedStatuses);

                if (allIssues.Count > 0)
                {
                    allIssues = allIssues.FindAll(s => s.SLAId.HasValue);
                }
                
                Dictionary<int, Pair<string,int>> result = new Dictionary<int, Pair<string,int>>();

                result.Add(Constants.SLAStatusBreach, new Pair<string, int>("Breached", 0));             
                result.Add(Constants.SLAStatusRed,  new Pair<string,int>("Red",0));
                result.Add(Constants.SLAStatusAmber,  new Pair<string,int>("Amber",0));
                result.Add(Constants.SLAStatusGreen,  new Pair<string,int>("Green",0));

                foreach(var issue in allIssues)
                {
                    if (issue.SLAStatus.HasValue && !result.ContainsKey(issue.SLAStatus.Value)) continue;

                    if (issue.SLAStatus.HasValue)
                        result[issue.SLAStatus.Value].Value++;
                    else
                        result[Constants.SLAStatusGreen].Value++;
                }
                
                var dataListProgressPie = new List<List<object>>();

                foreach (var status in result)
                {
                    dataListProgressPie.Add(new List<object>() { status.Value.Key, status.Value.Value});
                }

                return pieChart(dataListProgressPie, boardOption);
            }
            else if (boardOption == "sla-status-breakdown-list" && restrictTo == "sla")
            {
                SLAManager slaManager = new SLAManager(IssueManager);
                var allSlas = slaManager.GetAll();
                
                Dictionary<int, string> slaNames = new Dictionary<int, string>();

                foreach (var sla in allSlas)
                {
                    slaNames.Add(sla.Id, sla.Name);
                }

                filter.IncludeClosed = true;

                var allIssues = GeminiContext.Issues.GetIssues(ItemFilterManager.TransformFilter(filter), 0, Cache.CustomFields.GetAll(), closedStatuses);

                Dictionary<int, GeckoboardModel.SlaStatusBreakdown> result = new Dictionary<int, GeckoboardModel.SlaStatusBreakdown>();

                foreach (var issue in allIssues)
                {
                    if (!issue.SLAId.HasValue) continue;

                    var statusBreakdown = new GeckoboardModel.SlaStatusBreakdown();

                    if (result.ContainsKey(issue.SLAId.Value))
                    {
                        statusBreakdown = result[issue.SLAId.Value];
                    }
                    
                    switch (issue.SLAStatus)
                    {
                        case Constants.SLAStatusBreach:
                            statusBreakdown.Breached++;
                            break;
                        case Constants.SLAStatusRed:
                            statusBreakdown.Red++;
                            break;
                        case Constants.SLAStatusAmber:
                            statusBreakdown.Amber++;
                            break;
                        case Constants.SLAStatusGreen:
                            statusBreakdown.Green++;
                            break;
                        case null:
                            statusBreakdown.Green++;
                            break;
                    }

                    if (!result.ContainsKey(issue.SLAId.Value))
                    {
                        statusBreakdown.SlaName = slaNames[issue.SLAId.Value];
                        result.Add(issue.SLAId.Value, statusBreakdown);
                    }
                }
                
                WallboardHelper.TextWidget slaWidget = new WallboardHelper.TextWidget();
                StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", boardOption));

                foreach (var status in result)
                {
                    iBuffer.Append(string.Format("<tr> <td>{0}</td> <td title='breached' class='breached'>{1}</td><td title='red' class='red'>{2}</td><td title='amber' class='amber'>{3}</td><td title='green' class='green'>{4}</td> </tr>", status.Value.SlaName, status.Value.Breached, status.Value.Red, status.Value.Amber, status.Value.Green));
                }

                iBuffer.Append("</table>");

                slaWidget.item = new WallboardHelper.TextType[1];

                slaWidget.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };

                return slaWidget;
            }

            return null;
        }

        public object getSlaCount(string boardOption, IssuesFilter cardFilter, string restrictTo)
        {
            SLAManager slaManager = new SLAManager(IssueManager);
            var allSlas = slaManager.GetAll();

            DateTime todayDateTime = DateTime.Now;

            List<int> closedStatuses = MetaManager.StatusGetClosed();

            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);

            switch (boardOption)
            {
                case "open-sla":
                    filter.IncludeClosed = false;

                    if (restrictTo == "today")
                    {
                        filter.CreatedAfter = filter.CreatedBefore = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.CreatedBefore = filter.CreatedAfter = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.CreatedBefore = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if (restrictTo == "last-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.CreatedBefore = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
                case "closed-sla":
                    filter.IncludeClosed = true;
                    filter.Statuses = string.Join("|", closedStatuses);

                    if (restrictTo == "today")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.FinalClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if (restrictTo == "last-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.FinalClosedDate = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
            }

            var allIssues = GeminiContext.Issues.GetIssues(ItemFilterManager.TransformFilter(filter), 0, Cache.CustomFields.GetAll(), closedStatuses);

            WallboardHelper.TextWidget slaWidget = new WallboardHelper.TextWidget();
            slaWidget.item = new WallboardHelper.TextType[1];
            slaWidget.item[0] = new WallboardHelper.TextType() { text = "", type = 0 };

            StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", boardOption));

            Dictionary<string, double> result = new Dictionary<string, double>();

            foreach (var sla in allSlas)
            {
                var numberOfItems = allIssues.FindAll(s => s.SLAId == sla.Id).Count;
                
                if (numberOfItems > 0) result.Add(sla.Name, numberOfItems);
            }

            if (result.Count > 0)
            {
                foreach (var item in result.OrderByDescending(s => s.Key))
                {
                    iBuffer.Append(string.Format("<tr><td>{0}</td><td>{1}</td></tr>", item.Key, item.Value));
                }
            }

            var nonSlaCount = allIssues.Count(s => !s.SLAId.HasValue);

            if (nonSlaCount > 0)
            {
                iBuffer.Append(string.Format("<tr><td>Non Sla</td><td>{0}</td></tr>", nonSlaCount));
            }

            iBuffer.Append("</table>");

            slaWidget.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };

            return slaWidget;
        }

        public object getSlaTime(string boardOption, IssuesFilter cardFilter, string restrictTo)
        {
            SLAManager slaManager = new SLAManager(IssueManager);
            var allSlas = slaManager.GetAll();

            DateTime todayDateTime = DateTime.Now;

            List<int> closedStatuses = MetaManager.StatusGetClosed();
            
            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);
            filter.IncludeClosed = true;
            //Setting the filter for each widgets
            switch (boardOption)
            {
                case "average-sla-response":
                    if (restrictTo == "today")
                    {
                        filter.CreatedAfter = filter.CreatedBefore = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.CreatedBefore = filter.CreatedAfter = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.CreatedBefore = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if (restrictTo == "last-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.CreatedBefore = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
                case "average-sla-closing":
                    filter.Statuses = string.Join("|", closedStatuses);

                    if (restrictTo == "today")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.FinalClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if(restrictTo == "last-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.FinalClosedDate = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
            }

            List<IssueDto> allIssueDtos = IssueManager.GetFiltered(filter);

            WallboardHelper.TextWidget slaWidget = new WallboardHelper.TextWidget();
            StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", boardOption));

            Dictionary<string, double> result = new Dictionary<string, double>();

            foreach (var sla in allSlas)
            {
                if (boardOption == "average-sla-response")
                {
                    var issues = allIssueDtos.FindAll(s => s.Entity.SLAId == sla.Id);

                    double timeDifference = 0;

                    foreach(var issue in issues)
                    {
                        if (issue.Comments.Count > 0 && issue.Comments.Any(s => s.Entity.OriginatorType == IssueOriginatorType.Email))
                        {
                            var allEmailReplies = issue.Comments.FindAll(s => s.Entity.OriginatorType == IssueOriginatorType.Email).Select(s => s.Entity).OrderBy("Created");

                            var comment = allEmailReplies.First();
                            timeDifference += (comment.Created - issue.Created).TotalMinutes;
                        }
                    }
                    if (timeDifference > 0)
                    {
                        result.Add(sla.Name, timeDifference);
                    }
                }
                else if (boardOption == "average-sla-closing")
                {
                    var issues = allIssueDtos.FindAll(s => s.Entity.SLAId == sla.Id && s.ClosedDate.HasValue);
                    double timeDifference = 0;

                    foreach (var issue in issues)
                    {
                        timeDifference += (issue.ClosedDate.Value - issue.Created).TotalMinutes;
                    }

                    if (timeDifference > 0)
                    {
                        result.Add(sla.Name, timeDifference);
                    }
                }
            }

            foreach (var item in result.OrderByDescending(s => s.Value))
            {
                iBuffer.Append(string.Format("<tr><td>{0}</td><td>{1}</td></tr>", item.Key, Helper.GetFriendlyDate(item.Value)));
            }

            if (boardOption == "average-sla-response")
            {
                var nonSlaIssues = allIssueDtos.FindAll(s => !s.Entity.SLAId.HasValue);
                    
                double timeDifference = 0;

                foreach (var issue in nonSlaIssues)
                {
                    if (issue.Comments.Count > 0 && issue.Comments.Any(s => s.Entity.OriginatorType == IssueOriginatorType.Email))
                    {
                        var allEmailReplies = issue.Comments.FindAll(s => s.Entity.OriginatorType == IssueOriginatorType.Email).Select(s => s.Entity).OrderBy("Created");

                        var comment = allEmailReplies.First();

                        timeDifference += (comment.Created - issue.Created).TotalMinutes;
                    }
                }

                if (nonSlaIssues.Count > 0 && timeDifference > 0)
                {
                    iBuffer.Append(string.Format("<tr><td>Non Sla</td><td>{0}</td></tr>", Helper.GetFriendlyDate(timeDifference)));
                }
            }
            else if (boardOption == "average-sla-closing")
            {
                var nonSlaIssues = allIssueDtos.FindAll(s => !s.Entity.SLAId.HasValue);
                double timeDifference = 0;

                foreach (var issue in nonSlaIssues)
                {
                    timeDifference += (issue.ClosedDate.Value - issue.Created).TotalMinutes;
                }

                if (nonSlaIssues.Count > 0 && timeDifference > 0)
                {
                    iBuffer.Append(string.Format("<tr><td>Non Sla</td><td>{0}</td></tr>", Helper.GetFriendlyDate(timeDifference)));
                }
            }        

            iBuffer.Append("</table>");

            slaWidget.item = new WallboardHelper.TextType[1];

            slaWidget.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };

            return slaWidget;
        }

        public object getSlaTimeTotal(string boardOption, IssuesFilter cardFilter, string restrictTo)
        {
            SLAManager slaManager = new SLAManager(IssueManager);
            var allSlas = slaManager.GetAll();

            DateTime todayDateTime = DateTime.Now;

            List<int> closedStatuses = MetaManager.StatusGetClosed();

            //necessary to prevent updating the actual card filter
            IssuesFilter filter = new IssuesFilter(cardFilter);
            filter.IncludeClosed = true;

            //Setting the filter for each widgets
            switch (boardOption)
            {
                case "average-sla-response-total":
                    if (restrictTo == "today")
                    {
                        filter.CreatedAfter = filter.CreatedBefore = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.CreatedBefore = filter.CreatedAfter = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.CreatedBefore = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.CreatedBefore = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if (restrictTo == "last-month")
                    {
                        filter.CreatedAfter = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.CreatedBefore = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
                case "average-sla-closing-total":
                    filter.Statuses = string.Join("|", closedStatuses);

                    if (restrictTo == "today")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.ToString();
                    }
                    else if (restrictTo == "yesterday")
                    {
                        filter.FinalClosedDate = filter.InitialClosedDate = todayDateTime.Date.AddDays(-1).ToString();
                    }
                    else if (restrictTo == "this-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "this-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime).ToString();
                        filter.FinalClosedDate = todayDateTime.ToString();
                    }
                    else if (restrictTo == "last-week")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).ToString();
                        filter.FinalClosedDate = Helper.GetStartDayOfWeek(todayDateTime.AddDays(-7)).AddDays(6).ToString();
                    }
                    else if (restrictTo == "last-month")
                    {
                        filter.InitialClosedDate = Helper.GetStartDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                        filter.FinalClosedDate = Helper.GetLastDayOfMonth(todayDateTime.AddMonths(-1)).ToString();
                    }
                    break;
            }

            List<IssueDto> allIssueDtos = IssueManager.GetFiltered(filter);

            double timeDifference = 0;
            int numberOfComments = 0;

            foreach (var issue in allIssueDtos)
            {
                if (boardOption == "average-sla-response-total")
                {
                    if (issue.Comments.Count > 0 && issue.Comments.Any(s => s.Entity.OriginatorType == IssueOriginatorType.Email))
                    {
                        var allEmailReplies = issue.Comments.FindAll(s => s.Entity.OriginatorType == IssueOriginatorType.Email).Select(s => s.Entity).OrderBy("Created");

                        var comment = allEmailReplies.First();

                        timeDifference += (comment.Created - issue.Created).TotalMinutes;
                        numberOfComments++;
                    }
                }
                else if (boardOption == "average-sla-closing-total")
                {
                    timeDifference += (issue.ClosedDate.Value - issue.Created).TotalMinutes;
                    numberOfComments++;
                }
            }

            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();

            textW.item = new WallboardHelper.TextType[1];

            textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='{0} count'>{1}</div>", boardOption, timeDifference == 0 || numberOfComments == 0 ? "0d 0h 0m" : Helper.GetFriendlyDate(timeDifference / numberOfComments)), type = 0 };

            return textW;
        }
    }
}
