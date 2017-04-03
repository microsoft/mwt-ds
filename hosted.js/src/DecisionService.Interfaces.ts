
export interface DecisionServiceFeatures {

    ids: DecisionServiceReferenceId[];

    features: any;

    lock: boolean;
}

export interface DecisionServiceReferenceId {
    // optional
    site: string;

    id: string;
}

export interface DecisionServiceActionSet {
    id: DecisionServiceReferenceId;

    param: string;
}

export class DecisionServiceClientOptions {
    public hostname: string = 'ds.microsoft.com';
    public protocol: string = 'https';
}

export interface DecisionServiceRankRequest {
    site: string;

    shared: DecisionServiceFeatures;

    actions: DecisionServiceFeatures[];

    actionSets: DecisionServiceActionSet[];
}

export interface DecisionServicePage {
    /** Site id or domain. If omitted resolved from canonical or window.location. */
    site: string;

    /** Path to the article or object. */
    path: string;
}

export interface DecisionServiceOutcome {
    /** Site id or domain. If omitted resolved from canonical or window.location. */
    site: string;
    
    /** The event id for a decision. */
    eventId: string;
    
    /** The outcome for the referenced decision. Defaults to 1. */
    outcome: any;
}