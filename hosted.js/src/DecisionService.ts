// TODO: don't want to globally import this.
import $ = require('jquery/dist/jquery.slim.min.js');
import { DecisionServiceClient } from "./DecisionServiceClient";
import { DecisionServicePage, DecisionServiceClientOptions, DecisionServiceOutcome } from './DecisionService.Interfaces';


class DecisionServicePageUtil {
    public static discoverDecisionServicePage(): DecisionServicePage {
        var page: DecisionServicePage = <DecisionServicePage>{ site: null, path: null };

        // find Decision Service ID 
        // let the server deal with this
        // var ds_id = $("meta[property='microsoft:ds_id']");
        // if (ds_id !== null && ds_id !== undefined)
        // {
        //     page.id = ds_id.attr("content");

        //     var match = /^([^\/]+)\.(.+)$/.exec(page.id);
        //     if (match.length != 3) {
        //         page.site = match[1];
        //         page.id = match[2];
        //     }
        // }

        // find canonical
        var canonical = $('link[rel=canonical]');
        var url = window.location.href;
        if (canonical !== null) {
            var canonicalHref = canonical.attr("href");
            if (canonicalHref !== null)
                url = canonicalHref;
        }

        // extract path
        var parser = document.createElement('a');
        parser.href = url;
        page.path = parser.pathname;
        page.path = page.path.replace(/^\//, '');
        page.path = page.path.replace(/\/$/, '');

        if (page.site === null || page.site === undefined)
            page.site = parser.hostname;

        return page;
    }
}

/** Microsoft Decision Service */
class DecisionService {
    constructor() {
        this.client = new DecisionServiceClient();
    }

    public readonly client: DecisionServiceClient;

    private injectTrackingPixel(page: DecisionServicePage) {
        var img = document.createElement('img');
        img.src = `${this.client.options.protocol}://${this.client.options.hostname}/api/v2/track/${page.site}/${page.path}.png`;

        $(document.body).append(img);
    }

    /**
     * Track page view with Microsoft Decision Service.
     * @param page optional page description. If site and/or path are omitted the following source in order are use: canonical, window.location.
     */
    public trackPageView(page: DecisionServicePage = null) {
        if (page !== null && page !== undefined && page.site !== null && page.path !== null && page.site !== undefined && page.path !== undefined) {
            this.injectTrackingPixel(page);
            return;
        }

        // wait for page load for canonical to be ready
        $(document).ready(() => {
            var discovered = DecisionServicePageUtil.discoverDecisionServicePage();

            if (page === null || page === undefined)
                page = discovered;
            else if (page.site === null || page.path === null || page.site === undefined || page.path === undefined) {

                if (page.site === null || page.site === undefined)
                    page.site = discovered.site;

                if (page.path === null || page.path === undefined)
                    page.path = discovered.path;
            }

            this.injectTrackingPixel(page);
        });
    }

    public reportOutcome(outcome: DecisionServiceOutcome) {
        if (outcome.site !== null && outcome.site !== undefined) {
            this.client.reportOutcome(outcome);
            return;
        }

        // wait for page load for canonical to be ready
        $(document).ready(() => {
            outcome.site = DecisionServicePageUtil.discoverDecisionServicePage().site;
            this.client.reportOutcome(outcome);
        });
    }
};

(<any>window).decisionService = new DecisionService();
