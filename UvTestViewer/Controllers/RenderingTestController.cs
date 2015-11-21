using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using BambooClient;
using UvTestViewer.Models;
using UvTestViewer.Services;

namespace UvTestViewer.Controllers
{
    public class RenderingTestController : Controller
    {
        // GET: RenderingTest
        public async Task<ActionResult> Index(String vendor = null, String planKey = null, String branchKey = null)
        {
            planKey = planKey ?? ConfigurationManager.AppSettings["DefaultPlanKey"];

            var vendorValue = GpuVendor.Nvidia;
            if (vendor != null)
            {
                switch (vendor)
                {
                    case "intel":
                        vendorValue = GpuVendor.Intel;
                        break;

                    case "nvidia":
                        vendorValue = GpuVendor.Nvidia;
                        break;

                    case "amd":
                        vendorValue = GpuVendor.Amd;
                        break;

                    default:
                        return HttpNotFound("Unrecognized GPU vendor.");
                }
            }

            var service = new RenderingTestService();
            var overview = service.GetMostRecentRenderingTestOverview(vendorValue, planKey, branchKey) ?? new RenderingTestOverview();
            overview.SelectedPlanKey = planKey;
            overview.SelectedBranchKey = branchKey;
            overview.BambooPlans = await GetBambooPlans();
            return View(overview);
        }

        private async Task<IEnumerable<BambooPlan>> GetBambooPlans()
        {
            var excludedPlanKeys = (ConfigurationManager.AppSettings["BambooExcludedPlanKeys"] ?? String.Empty).Split(';');

            var bambooBaseUri = new Uri(ConfigurationManager.AppSettings["BambooBaseUri"]);
            using (var bamboo = new BambooHttpClient(bambooBaseUri))
            {
                var result = new List<BambooPlan>();

                var queriedPlans = await bamboo.EnumeratePlans();
                foreach (var queriedPlan in queriedPlans)
                {
                    if (excludedPlanKeys.Contains(queriedPlan.Key))
                        continue;

                    var plan = new BambooPlan();
                    plan.Name = queriedPlan.ShortName;
                    plan.PlanKey = queriedPlan.Key;
                    plan.Branches = await GetBambooBranches(bamboo, queriedPlan.Key);
                    result.Add(plan);
                }

                return result;
            }
        }

        private async Task<IEnumerable<BambooBranch>> GetBambooBranches(BambooHttpClient bamboo, String planKey)
        {
            var result = new List<BambooBranch>();
            result.Add(new BambooBranch() { Name = "master", PlanKey = planKey, BranchKey = null });

            var queriedBranches = await bamboo.EnumeratePlanBranches(planKey);
            foreach (var queriedBranch in queriedBranches)
            {
                var branch = new BambooBranch();
                branch.Name = queriedBranch.ShortName;
                branch.PlanKey = planKey;
                branch.BranchKey = queriedBranch.Key;
                result.Add(branch);
            }

            return result;
        }
    }
}