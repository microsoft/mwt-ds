import $ = require('jquery/dist/jquery.slim.min.js');
import { DecisionServiceRankRequest, DecisionServiceClientOptions, DecisionServiceOutcome } from './DecisionService.Interfaces';
import { DecisionServiceRequestUtil } from './DecisionServiceRequestUtil';

export interface DecisionServiceRankingOptions {
    requests: DecisionServiceRankRequest[];

    /** Requests details for each action. Defaults to false. */
    detail: boolean;
    
    /** Disables user tracking. Defaults to false. */
    disableUserTracking: boolean;

    success: (json: any) => any;

    error: (xhr: any, err: any) => any;
}

export class DecisionServiceClient {
    constructor(options: DecisionServiceClientOptions = new DecisionServiceClientOptions) {
        this.options = options;
    }

    // properties can still be modified, which is fine.
    readonly options: DecisionServiceClientOptions;

    public rank(opts: DecisionServiceRankingOptions) {
        var path = DecisionServiceRequestUtil.combine(opts.requests);

        var url = `${this.options.protocol}://${this.options.hostname}/api/v2/rank/${path}.js?`;
        if (opts.detail !== null && opts.detail !== undefined && opts.detail)
            url += 'detail=1&';
        if (opts.disableUserTracking !== null && opts.disableUserTracking !== undefined && opts.disableUserTracking)
            url += 'dnt=1&';

        url += 'callback=?';

        // http://api.jquery.com/jquery.getjson/
        $.getJSON(url, function (json) { console.log(json); });
    }

    public reportOutcome(outcome: DecisionServiceOutcome) {
        $.ajax({
            url: `${this.options.protocol}://${this.options.hostname}/api/outcome/${outcome.site}/${outcome.eventId}`,
            method: 'POST',
            async: true,
            contentType: "application/json",
            data: JSON.stringify(outcome),
            error: function (xhr, err) {
                console.log(`Microsoft Decision Service: Outcome reporting failed. State ${xhr.readyState} status: ${xhr.status} response: ${xhr.responseText}`);
            }
        });
    }

    public registerUrl(siteId: string, docId: string, url: string, authKey: string) {
        // TODO: AJAX
    }
}
