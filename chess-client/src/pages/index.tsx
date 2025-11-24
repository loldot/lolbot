import { lazy, Suspense } from "react"

// TypeScript declarations for web components
declare global {
    namespace JSX {
        interface IntrinsicElements {
            'ui-tabs': any;
            'ui-tab-panel': any;
            'ui-card': any;
        }
    }
}

// Lazy load components
const NewGame = lazy(() => import("./game/new"));
const MasterGameView = lazy(() => import("./game/master"));
const TestResults = lazy(() => import("./test-results"));
const PositionTrends = lazy(() => import("./position-trends"));

const Home = () => {
    return (
        <div style={{ padding: '1rem' }}>
            <h1>Lolbot Chess Engine</h1>

            <ui-tabs>
                {/* <ui-tab-panel title="New Game">
                    <h3 slot="header">New Game</h3>
                    <section slot="content" className="block md">
                        
                        <Suspense fallback={
                            <ui-card>
                                <span slot="icon">⏳</span>
                                <span slot="header">Loading...</span>
                                <div>Starting new game...</div>
                            </ui-card>
                        }>
                            <NewGame />
                        </Suspense>
                    </section>
                </ui-tab-panel> */}

                <ui-tab-panel title="Master Game">
                    <section slot="content" className="block md">
                        <h3>Watch Master Game</h3>
                        <Suspense fallback={
                            <ui-card>
                                <span slot="icon">⏳</span>
                                <span slot="header">Loading...</span>
                                <div>Loading game...</div>
                            </ui-card>
                        }>
                            <MasterGameView />
                        </Suspense>
                    </section>
                </ui-tab-panel>

                <ui-tab-panel title="Test Results">
                    <section slot="content" className="block md">
                        <Suspense fallback={
                            <ui-card>
                                <span slot="icon">⏳</span>
                                <span slot="header">Loading...</span>
                                <div>Loading test results...</div>
                            </ui-card>
                        }>
                            <TestResults />
                        </Suspense>
                    </section>
                </ui-tab-panel>

                <ui-tab-panel title="Position Trends">
                    <section slot="content" className="block md">
                        <h3>Performance Trends</h3>
                        <Suspense fallback={
                            <ui-card>
                                <span slot="icon">⏳</span>
                                <span slot="header">Loading...</span>
                                <div>Preparing charts...</div>
                            </ui-card>
                        }>
                            <PositionTrends />
                        </Suspense>
                    </section>
                </ui-tab-panel>
            </ui-tabs>
        </div>
    );
}

export default Home;