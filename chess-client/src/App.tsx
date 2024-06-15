import { Suspense } from 'react';

import routes from '~react-pages';
import { useRoutes } from 'react-router';

const App = () => {
  console.log(routes);
  return (
    <Suspense fallback={<p>Loading...</p>}>
      {useRoutes(routes)}
    </Suspense>
  );
}

export default App
