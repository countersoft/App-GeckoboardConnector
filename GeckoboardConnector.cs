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

namespace GeckoboardConnector
{

    public class GeckoboardRoutes : IAppRoutes
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapHttpRoute(null, "api/wallboard/{cardId}/{boardoption}/{limit}", new { controller = "Geckoboard", action = "Get", limit = RouteParameter.Optional }, new { httpMethod = new HttpMethodConstraint(new string[] { "GET" }) });
        }
    }

    public class GeckoboardController : BaseApiController
    {
        [System.Web.Mvc.HttpGet]
        public object Get(int cardId, string boardOption, int limit = 0)
        {
            object result = string.Empty;
            
            string username = string.Empty;
            
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

            var filter = card.Filter;
            
            var issues = new List<IssueDto>();

            WallboardHelper.RAGNumberWidget rag = new WallboardHelper.RAGNumberWidget();
            
            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();
            
            WallboardHelper.FunnelWidget funnel = new WallboardHelper.FunnelWidget();

            var closed = 0;
            
            var x = 0;

            if (boardOption.HasValue()) boardOption = boardOption.ToLowerInvariant();
            
            switch (boardOption)
            {
                case "progress":
                    issues = IssueManager.GetFiltered(filter);

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
                    var open = IssueManager.GetFiltered(filter).Count(i => !i.IsClosed);
                    
                    textW.item = new WallboardHelper.TextType[1];
                    
                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='opened-count'>{0}</div>", open.ToString()), type = 0 };
                    
                    result = textW;
                    
                    break;
                case "closed-count":
                    closed = IssueManager.GetFiltered(filter).Count(i => i.IsClosed);
                    
                    textW.item = new WallboardHelper.TextType[1];
                    
                    textW.item[0] = new WallboardHelper.TextType() { text = string.Format("<div class='closed-count'>{0}</div>", closed.ToString()), type = 0 };
                    
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
                    issues = IssueManager.GetFiltered(filter);
                    
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
                default:
                    result = "Unrecognized action specified: " + boardOption;
                    break;
            }

            return result;
        }

        public object pieChart(List<List<object>> dataList)
        {
            WallboardHelper.PieChartWidget pie = new WallboardHelper.PieChartWidget();
            
            pie.chart = new WallboardHelper.PieChartWidget.Chart();
            
            pie.chart.renderTo = "container";
            
            pie.chart.backgroundColor = "rgba(35,37,38,100)";
            
            pie.chart.borderColor = "container";

            pie.chart.lineColor = "rgba(35,37,38,100)";
            
            pie.chart.plotBackgroundColor = "rgba(35,37,38,0)";
            
            pie.chart.plotBorderColor = "rgba(35,37,38,100)";

            pie.tooltip = new WallboardHelper.PieChartWidget.ToolTip();
            
            pie.tooltip.formatter = "function() {  return '<b>' + this.point.name + '</b>' + Math.round(this.percentage) + '%'; } ";

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

            return pie;
        }

        public object items(IssuesFilter filter, int limit, string option)
        {
            WallboardHelper.TextWidget textW = new WallboardHelper.TextWidget();

            IEnumerable<IssueDto> tmp_issues;
            
            var issues = new List<IssueDto>();

            if (option == "closed-recently")
            {
                if (filter.IncludeClosed)
                {
                    filter.SystemFilter = IssuesFilter.SystemFilterTypes.RecentlyClosedIssues;
                    
                    issues = IssueManager.GetFiltered(filter);
                    
                    tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
                }
                else
                    tmp_issues = new List<IssueDto>();
            }
            else if (option == "opened-recently")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.RecentlyCreatedIssues;
                
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-nextweek")
            {
                DateTime input = DateTime.Today;
                
                int delta = DayOfWeek.Monday - input.DayOfWeek;
                
                DateTime monday = input.AddDays(delta);

                filter.InitialDueDate = monday.AddDays(7).ToShortDateString();
                
                filter.FinalDueDate = monday.AddDays(13).ToShortDateString();
                
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-thisweek")
            {
                DateTime input = DateTime.Today;
                
                int delta = DayOfWeek.Monday - input.DayOfWeek;
                
                DateTime monday = input.AddDays(delta);

                filter.InitialDueDate = monday.ToShortDateString();
                
                filter.FinalDueDate = monday.AddDays(7).ToShortDateString();
                
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-tomorrow")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.DueTomorrowIssues;
                
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "due-today")
            {
                filter.SystemFilter = IssuesFilter.SystemFilterTypes.DueTodayIssues;
                
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "closed")
            {
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.FindAll(i => i.IsClosed).OrderByDescending(i => i.Created).Take((limit == 0 ? issues.Count : limit));
            }
            else if (option == "opened")
            {
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.FindAll(i => !i.IsClosed).OrderByDescending(i => i.Created).Take((limit == 0 ? issues.Count : limit));
            }
            else
            {
                issues = IssueManager.GetFiltered(filter);
                
                tmp_issues = issues.OrderByDescending(i => i.Revised).Take((limit == 0 ? issues.Count : limit));
            }

            StringBuilder iBuffer = new StringBuilder(string.Format("<table class='items-list {0}'>", option));

            if (tmp_issues.Count() > 0)
            {
                foreach (var issue in tmp_issues)
                {
                    iBuffer.Append(string.Format("<tr><td>{0}</td><td>{1}</td></tr>", issue.IssueKey, issue.Title));
                }
            }

            iBuffer.Append("</table>");
            
            textW.item = new WallboardHelper.TextType[1];
            
            textW.item[0] = new WallboardHelper.TextType() { text = iBuffer.ToString(), type = 0 };
            
            return textW;
        }
    }
}
