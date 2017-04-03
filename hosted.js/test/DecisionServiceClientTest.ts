import "mocha";
import { assert } from "chai";

// TODO: use namespaces?
import { DecisionServiceRequestUtil } from "../src/DecisionServiceRequestUtil";
import { DecisionServiceRankRequest } from "../src/DecisionService.Interfaces";

describe("DecisionServiceRank", () => {
    it("Simple actions", () => {
        var request = {
            site: 'abc',
            actions: [
                { ids: [{ id: '1' }] },
                { ids: [{ id: '2' }] },
            ],
            actionSets: null,
            shared: null
        };

        assert.equal(DecisionServiceRequestUtil.requestToPath(<DecisionServiceRankRequest>request), '~abc/1/2');
    });

    it("Shared with features", () => {
        var request = {
            site: 'b234',
            shared: {
                ids: [{ id: '4' }],
                features: { y: { d: true, f: true, g: true, h: false } }
            },
            actions: [{ ids: [{ id: 'def' }] }],
            actionSets: null
        };

        assert.equal(DecisionServiceRequestUtil.requestToPath(<DecisionServiceRankRequest>request), '~b234!4;y~d,f,g/def');
    });

    it("Action sets", () => {
        var request = {
            site: 'b234',
            shared: undefined,
            actions: undefined,
            actionSets: [
                { id: { site: 'articles', id: 'trending' } },
                { id: { id: 'tag' }, param: 'kanye' }
            ]
        };

        assert.equal(DecisionServiceRequestUtil.requestToPath(<DecisionServiceRankRequest>request), '~b234/$articles$trending/$tag=kanye');
    });
});