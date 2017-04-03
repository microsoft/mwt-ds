
import { DecisionServiceRankRequest, DecisionServiceReferenceId, DecisionServiceActionSet, DecisionServiceFeatures } from './DecisionService.Interfaces';

/** Converts Decision Service requests into URL format. */
export class DecisionServiceRequestUtil {
    public static requestToPath(that: DecisionServiceRankRequest): string {
        var elements = [`~${that.site}`];

        if (that.shared !== null && that.shared !== undefined) {
            var path = DecisionServiceRequestUtil.featuresToPath(that.shared);
            // ! denotes shared part
            elements[0] += '!' + path;
        }

        if (that.actions !== null && that.actions !== undefined)
            elements = elements.concat(that.actions.map(DecisionServiceRequestUtil.featuresToPath));

        // $ denotes action set
        if (that.actionSets !== null && that.actionSets !== undefined)
            elements = elements.concat(that.actionSets.map(DecisionServiceRequestUtil.actionSetToPath));

        return elements.join('/');
    }

    private static featuresToPath(that: DecisionServiceFeatures): string {
        if (that.lock !== null && that.lock !== undefined && that.lock)
            throw Error("Microsoft Decision Service: 'lock=true' is not supported in the URL format.")

        var path = '';

        if (that.ids !== null && that.ids !== undefined)
            path = that.ids.map(DecisionServiceRequestUtil.idToPath).join(',');

        if (that.features !== null && that.features !== undefined)
            path += ';' + DecisionServiceRequestUtil.featureToPath(null, that.features).join(',');

        return path;
    }

    private static featureToPath(property: string, value: any): string[] {
        if (typeof (value) === 'boolean' || value instanceof Boolean) {
            if (property === null)
                throw Error("Microsoft Decision Service: feature cannot be a plain boolean. Name missing.");

            return value ? [property] : [];
        }

        if (typeof (value) === 'number' || value instanceof Number) {
            if (property === null)
                throw Error("Microsoft Decision Service: feature cannot be a plain number. Name missing.");

            return [`${property}=${value}`];
        }

        var elements = [];
        for (let i in value)
            elements = elements.concat(this.featureToPath(i, value[i]));

        if (property !== null)
            elements = [`${property}~${elements.join(',')}`];

        return elements;
    }

    private static idToPath(that: DecisionServiceReferenceId): string {
        if (that.id.indexOf('/') !== -1)
            throw Error(`Decision Service reference ids must not contain / if used in URL. Id: '${that.id}'`);

        return that.site === null || that.site === undefined ?
            that.id :
            `${that.site}$${that.id}`;
    }

    private static actionSetToPath(that: DecisionServiceActionSet): string {
        var path = '$' + DecisionServiceRequestUtil.idToPath(that.id);

        if (that.param !== null && that.param !== undefined)
            path += `=${that.param}`;

        return path;
    }

    // multiple 
    public static combine(requests: DecisionServiceRankRequest[]) {
        if (requests === null || requests.length == 0)
            throw Error('Need at least 1 request');

        return requests.map(DecisionServiceRequestUtil.requestToPath).join('/');
    }
}
